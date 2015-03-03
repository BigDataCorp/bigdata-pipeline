# Configuration

## Configuration Parameters


### logFilename
`logFilename=<string>`

Default value: `${basedir}/log/BigDataPipeline.log`


### logLevel
`logLevel=<string>`

Default value: `Info`

### config
`config=<string>`

Default value: `empty`

Address to a downloadable configuration file with json configuration options.

### workFolder
`workFolder=<string>`

Default value: `empty`

### pluginFolder
`pluginFolder=<string>`

Default value: `empty`

### storageModule
`storageModule=<string>`

Default value: `empty`

* MongoDbStorageModule
* SqliteStorageModule (windows only)

### storageConnectionString
`storageConnectionString=<string>`

Default value: `empty`

Example: 
```
mongodb://[username:password@]host1[:port1][,host2[:port2],...[,hostN[:portN]]][/[database][?options]]
```

### storageDatabaseName
`storageDatabaseName=<string>`

Default value: `BigdataPipeline`

### actionLoggerOutputModule
`actionLoggerOutputModule=<string>`

Default value: `BigDataPipeline.Core.ActionLoggerInMemoryOutput`

### actionLoggerDatabaseName
`actionLoggerDatabaseName=<string>`

Default value: `empty`

If not configured, the storeageDatabaseName will be used

### actionLoggerConnectionString
`actionLoggerConnectionString=<string>`

Default value: `empty`

If not configured, the storageConnectionString will be used.

### accessControlModule
`accessControlModule=<string>`

Default value: `empty`

If not configured or empty, the first loaded accessControl module will be used.

* BigDataPipeline.DummyAccessControlModule

### webInterfaceEnabled
`webInterfaceEnabled=<boolean>`

Default value: `empty`

If enabled, pileline will start to listen to the specified address and port serve a web interface and web api.

### webInterfacePort
`webInterfacePort=<integer>`

Default value: `8080`

### webVirtualDirectoryPath
`webVirtualDirectoryPath=<string>`

Default value: `/bigdatapipeline`

### webInterfaceDisplayOnBrowserOnStart
`webInterfaceDisplayOnBrowserOnStart=<bool>`

Default value: `false`

If enabled, will try to open the browser with the web interface start page.


### serviceUpdateIntervalInSeconds
`serviceUpdateIntervalInSeconds=<integer>`

Default value: `60`


### webInterfacePort
`webInterfacePort=<integer>`

Default value: `8080`

### actionLogLevel
`actionLogLevel=<string>`

Default value: `Info`

* Trace
* Debug
* Info
* Success
* Warn
* Error
* Fatal

### actionLogStackTrace
`actionLogStackTrace=<boolean>`

Default value: `false`