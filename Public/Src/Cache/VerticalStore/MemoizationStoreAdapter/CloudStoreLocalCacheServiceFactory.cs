// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.ContractsLight;
using System.Threading.Tasks;
using BuildXL.Cache.ContentStore.Distributed.NuCache;
using BuildXL.Cache.ContentStore.Interfaces.Logging;
using BuildXL.Cache.ContentStore.Service.Grpc;
using BuildXL.Cache.ContentStore.Sessions;
using BuildXL.Cache.ContentStore.SQLite;
using BuildXL.Cache.ContentStore.Stores;
using BuildXL.Cache.Interfaces;
using BuildXL.Cache.MemoizationStore.Sessions;
using BuildXL.Cache.MemoizationStore.Stores;
using BuildXL.Utilities;
using AbsolutePath = BuildXL.Cache.ContentStore.Interfaces.FileSystem.AbsolutePath;

namespace BuildXL.Cache.MemoizationStoreAdapter
{
    /// <summary>
    /// The Cache Factory for CASaaS backed Cache implementation based on MemoizationStore.
    /// </summary>
    public class CloudStoreLocalCacheServiceFactory : ICacheFactory
    {
        // CasServiceCacheFactory JSON CONFIG DATA
        // {
        //     "Assembly":"BuildXL.Cache.MemoizationStoreAdapter",
        //     "Type":"BuildXL.Cache.MemoizationStoreAdapter.CloudStoreLocalCacheServiceFactory",
        //     "CacheId":"{0}",
        //     "CacheName":{1},
        //     "ConnectionsPerSession":"{2}",
        //     "MaxStrongFingerprints":{3},
        //     "MetadataRootPath":"{4}",
        //     "MetadataLogPath":"{5}",
        //     "ScenarioName":{6},
        //     "GrpcPort":{7},
        // Sensitivity is deprecated and left for backward compatibility
        //     "Sensitivity":{8},
        //     "BackupLKGCache":{9},
        //     "CheckCacheIntegrityOnStartup":{10}
        //     "SingleInstanceTimeoutInSeconds":{11}
        //     "SynchronizationMode":{12},
        //     "LogFlushIntervalSeconds":{13},
        //     "ReplaceExistingOnPlaceFile":{14},
        //     "VfsCasRoot": "{15}",
        //     "UseVfsSymlinks": "{16}",
        // }
        private sealed class Config
        {
            /// <summary>
            /// The Id of the cache instance.
            /// </summary>
            [DefaultValue(typeof(CacheId))]
            public CacheId CacheId { get; set; }

            /// <summary>
            /// Max number of CasEntries entries.
            /// </summary>
            [DefaultValue(500000)]
            public uint MaxStrongFingerprints { get; set; }

            /// <summary>
            /// Root path for storing metadata entries.
            /// </summary>
            public string MetadataRootPath { get; set; }

            /// <summary>
            /// Path to the log file for the cache.
            /// </summary>
            public string MetadataLogPath { get; set; }

            /// <summary>
            /// Name of one of the named caches owned by CASaaS.
            /// </summary>
            public string CacheName { get; set; }

            /// <summary>
            /// Connections to CASaasS per session.
            /// </summary>
            [DefaultValue(16)]
            public uint ConnectionsPerSession { get; set; }

            /// <summary>
            /// How many seconds each call should wait for a CASaaS connection before retrying.
            /// </summary>
            [DefaultValue(5)]
            public uint ConnectionRetryIntervalSeconds { get; set; }

            /// <summary>
            /// How many times each call should retry connecting to CASaaS before timing out.
            /// </summary>
            [DefaultValue(12)]
            public uint ConnectionRetryCount { get; set; }

            /// <summary>
            /// A custom scenario to connect to for the CAS service.
            /// </summary>
            [DefaultValue(null)]
            public string ScenarioName { get; set; }

            /// <summary>
            /// A custom scenario to connect to for the CAS service. If set, overrides GrpcPortFileName.
            /// </summary>
            [DefaultValue(0)]
            public uint GrpcPort { get; set; }

            /// <summary>
            /// Custom name of the memory-mapped file to read from to find the GRPC port used for the CAS service.
            /// </summary>
            [DefaultValue(null)]
            public string GrpcPortFileName { get; set; }

            /// <nodoc />
            [DefaultValue(false)]
            public bool GrpcTraceOperationStarted { get; set; }

            /// <summary>
            /// The sensitivity of the cache session.
            /// </summary>
            [DefaultValue(null)]
            public string Sensitivity { get; set; }

            /// <summary>
            ///     Create a backup of the last known good cache at startup. This step has a 
            ///     startup cost but allows better recovery in case the cache detects
            ///     corruption at startup.
            /// </summary>
            [DefaultValue(false)]
            public bool BackupLKGCache { get; set; }

            /// <summary>
            ///     The cache will check its integrity on startup. If the integrity check
            ///     fails, the corrupt cache data is thrown out and use a LKG data backup
            ///     is used. If a backup is unavailable the cache starts from scratch.
            /// </summary>
            [DefaultValue(false)]
            public bool CheckCacheIntegrityOnStartup { get; set; }

            /// <summary>
            /// Duration to wait for exclusive access to the cache directory before timing out.
            /// </summary>
            [DefaultValue(ContentStoreConfiguration.DefaultSingleInstanceTimeoutSeconds)]
            public uint SingleInstanceTimeoutInSeconds { get; set; }

            /// <summary>
            /// Controls the synchronization mode for writes to the database.
            /// </summary>
            [DefaultValue(null)]
            public string SynchronizationMode { get; set; }

            /// <summary>
            /// Duration to wait for exclusive access to the cache directory before timing out.
            /// </summary>
            [DefaultValue(0)]
            public uint LogFlushIntervalSeconds { get; set; }

            [DefaultValue(false)]
            public bool ReplaceExistingOnPlaceFile { get; set; }

            [DefaultValue(false)]
            public bool EnableMetadataServer { get; set; }

            [DefaultValue(false)]
            public bool UseRocksDbMemoizationStore { get; set; }

            [DefaultValue(60 * 60)]
            public int RocksDbMemoizationStoreGarbageCollectionIntervalInSeconds { get; set; }

            [DefaultValue(500_000)]
            public int RocksDbMemoizationStoreGarbageCollectionMaximumNumberOfEntriesToKeep { get; set; }

            /// <summary>
            /// Path to the root of VFS cas
            /// </summary>
            [DefaultValue(null)]
            public string VfsCasRoot { get; set; }

            /// <summary>
            /// Indicates whether symlinks should be used to specify VFS files
            /// </summary>
            [DefaultValue(true)]
            public bool UseVfsSymlinks { get; set; } = true;

            /// <nodoc />
            [DefaultValue(false)]
            public bool UseRoxisMetadataStore { get; set; }

            /// <nodoc />
            [DefaultValue("")]
            public string RoxisMetadataStoreHost { get; set; }

            /// <nodoc />
            [DefaultValue(-1)]
            public int RoxisMetadataStorePort { get; set; }
        }

        /// <inheritdoc />
        public async Task<Possible<ICache, Failure>> InitializeCacheAsync(ICacheConfigData cacheData, Guid activityId)
        {
            Contract.Requires(cacheData != null);

            var possibleCacheConfig = cacheData.Create<Config>();
            if (!possibleCacheConfig.Succeeded)
            {
                return possibleCacheConfig.Failure;
            }

            Config cacheConfig = possibleCacheConfig.Result;

            try
            {
                Contract.Assert(cacheConfig.CacheName != null);
                var logPath = new AbsolutePath(cacheConfig.MetadataLogPath);
                var logger = new DisposeLogger(() => new EtwFileLog(logPath.Path, cacheConfig.CacheId), cacheConfig.LogFlushIntervalSeconds);

                logger.Debug($"Creating CASaaS backed LocalCache using cache name: {cacheConfig.CacheName}");

                var cache = CreateCache(cacheConfig, logPath, logger);

                var startupResult = await cache.StartupAsync();
                if (!startupResult.Succeeded)
                {
                    logger.Error($"Error while initializing the cache [{cacheConfig.CacheId}]. Failure: {startupResult.Failure}");
                    return startupResult.Failure;
                }

                logger.Debug("Successfully started CloudStoreLocalCacheService client.");
                return cache;
            }
            catch (Exception e)
            {
                return new CacheConstructionFailure(cacheConfig.CacheId, e);
            }
        }

        private static MemoizationStoreAdapterCache CreateCache(Config cacheConfig, AbsolutePath logPath, ILogger logger)
        {
            ServiceClientRpcConfiguration rpcConfiguration;
            if (cacheConfig.GrpcPort != 0)
            {
                rpcConfiguration = new ServiceClientRpcConfiguration((int)cacheConfig.GrpcPort);
            }
            else
            {
                var factory = new MemoryMappedFileGrpcPortSharingFactory(logger, cacheConfig.GrpcPortFileName);
                var portReader = factory.GetPortReader();
                var port = portReader.ReadPort();

                rpcConfiguration = new ServiceClientRpcConfiguration(port);
            }

            var serviceClientConfiguration = new ServiceClientContentStoreConfiguration(cacheConfig.CacheName, rpcConfiguration, cacheConfig.ScenarioName)
            {
                RetryCount = cacheConfig.ConnectionRetryCount,
                RetryIntervalSeconds = cacheConfig.ConnectionRetryIntervalSeconds,
                TraceOperationStarted = cacheConfig.GrpcTraceOperationStarted,
            };

            MemoizationStore.Interfaces.Caches.ICache localCache;
            if (cacheConfig.EnableMetadataServer)
            {
                localCache = LocalCache.CreateRpcCache(logger, serviceClientConfiguration);
            }
            else
            {
                var metadataRootPath = new AbsolutePath(cacheConfig.MetadataRootPath);

                localCache = LocalCache.CreateRpcContentStoreInProcMemoizationStoreCache(logger,
                    metadataRootPath,
                    serviceClientConfiguration,
                    CreateInProcMemoizationStoreConfiguration(cacheConfig, metadataRootPath));
            }

            var statsFilePath = new AbsolutePath(logPath.Path + ".stats");
            if (!string.IsNullOrEmpty(cacheConfig.VfsCasRoot))
            {
                logger.Debug($"Creating virtualized cache");

                localCache = new VirtualizedContentCache(localCache, new ContentStore.Vfs.VfsCasConfiguration.Builder()
                {
                    RootPath = new AbsolutePath(cacheConfig.VfsCasRoot),
                    UseSymlinks = cacheConfig.UseVfsSymlinks
                }.Build());
            }

            var cache = new MemoizationStoreAdapterCache(cacheConfig.CacheId, localCache, logger, statsFilePath, cacheConfig.ReplaceExistingOnPlaceFile);
            return cache;
        }

        private static MemoizationStoreConfiguration CreateInProcMemoizationStoreConfiguration(Config cacheConfig, AbsolutePath cacheRootPath)
        {
            if (cacheConfig.UseRoxisMetadataStore)
            {
                var roxisClientConfiguration = new Roxis.Client.RoxisClientConfiguration();

                if (!string.IsNullOrEmpty(cacheConfig.RoxisMetadataStoreHost))
                {
                    roxisClientConfiguration.GrpcHost = cacheConfig.RoxisMetadataStoreHost;
                }

                if (cacheConfig.RoxisMetadataStorePort > 0)
                {
                    roxisClientConfiguration.GrpcPort = cacheConfig.RoxisMetadataStorePort;
                }

                return new RoxisMemoizationDatabaseConfiguration()
                {
                    MetadataClientConfiguration = roxisClientConfiguration,
                };
            }

            if (cacheConfig.UseRocksDbMemoizationStore)
            {
                return new RocksDbMemoizationStoreConfiguration()
                {
                    Database = new RocksDbContentLocationDatabaseConfiguration(cacheRootPath / "RocksDbMemoizationStore")
                    {
                        CleanOnInitialize = false,
                        GarbageCollectionInterval = TimeSpan.FromSeconds(cacheConfig.RocksDbMemoizationStoreGarbageCollectionIntervalInSeconds),
                        MetadataGarbageCollectionEnabled = true,
                        MetadataGarbageCollectionMaximumNumberOfEntriesToKeep = cacheConfig.RocksDbMemoizationStoreGarbageCollectionMaximumNumberOfEntriesToKeep,
                        OnFailureDeleteExistingStoreAndRetry = true,
                        LogsKeepLongTerm = true,
                    },
                };
            }
            else
            {
                var memoConfig = new SQLiteMemoizationStoreConfiguration(cacheRootPath)
                {
                    MaxRowCount = cacheConfig.MaxStrongFingerprints,
                    SingleInstanceTimeoutSeconds = (int)cacheConfig.SingleInstanceTimeoutInSeconds
                };

                memoConfig.Database.BackupDatabase = cacheConfig.BackupLKGCache;
                memoConfig.Database.VerifyIntegrityOnStartup = cacheConfig.CheckCacheIntegrityOnStartup;

                if (!string.IsNullOrEmpty(cacheConfig.SynchronizationMode))
                {
                    memoConfig.Database.SyncMode = (SynchronizationMode)Enum.Parse(typeof(SynchronizationMode), cacheConfig.SynchronizationMode, ignoreCase: true);
                }

                return memoConfig;
            }
        }

        /// <inheritdoc />
        public IEnumerable<Failure> ValidateConfiguration(ICacheConfigData cacheData)
        {
            return CacheConfigDataValidator.ValidateConfiguration<Config>(cacheData, cacheConfig =>
            {
                var failures = new List<Failure>();
                failures.AddFailureIfNullOrWhitespace(cacheConfig.CacheId, nameof(cacheConfig.CacheId));
                failures.AddFailureIfNullOrWhitespace(cacheConfig.CacheName, nameof(cacheConfig.CacheName));
                failures.AddFailureIfNullOrWhitespace(cacheConfig.MetadataLogPath, nameof(cacheConfig.MetadataLogPath));
                return failures;
            });
        }
    }
}
