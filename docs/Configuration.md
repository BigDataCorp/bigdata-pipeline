# Configuration Parameters

Server specific parameters to configure the service.


## Server basic configuration


### serviceUpdateIntervalInSeconds
`serviceUpdateIntervalInSeconds=<integer>`

Default value: `60`


### logFilename
`logFilename=<string>`

Default value: `${basedir}/log/BigDataPipeline.log`


### logLevel
`logLevel=<string>`

Default value: `Info`

* Trace
* Debug
* Info
* Success
* Warn
* Error
* Fatal


### config
`config=<string>`

Default value: `empty`


Address to an external file with a json format configuration options. At service start up the file will be loaded and parsed.
**Note:** This configuration takes precedence over the configurations found in the appSettings area of app.config file.

This address could be a local file system location or a web location. Examples:
* `http://somewhere.com/myconfiguration.json`
* `./myconfiguration.json`
* `c:\my configuration files\myconfiguration.json`


Also, a list of file can be provided as comma separated values. Example: 

```
"config": "http://somewhere.com/myconfiguration.json, c:\my configuration files\myconfiguration.json"
```

File format example
```
{
    "storageModule": "MongoDbStorageModule",
    "storageConnectionString": "..."
}
```


### configAbortOnError
`configAbortOnError=<boolean>`

Default value: `true`

If the server should tolerate and ignore external file configuration load or parse errors.


### workFolder
`workFolder=<string>`

Default value: `${basedir}/work`


### pluginFolder
`pluginFolder=<string>`

Default value: `${basedir}/plugins`


### accessControlModule
`accessControlModule=<string>`

Default value: `empty`

If not configured or empty, the first loaded accessControl module will be used.

* BigDataPipeline.DummyAccessControlModule


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

* ActionLoggerInMemoryOutput
* MongoDbActionLoggerOutput
* SqliteActionLoggerOutput (windows only)


### actionLoggerDatabaseName
`actionLoggerDatabaseName=<string>`

Default value: `empty`

If not configured, the storeageDatabaseName will be used


### actionLoggerConnectionString
`actionLoggerConnectionString=<string>`

Default value: `empty`

If not configured, the storageConnectionString will be used.


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


## Web server interface

Web server interface parameters


### webInterfaceEnabled
`webInterfaceEnabled=<boolean>`

Default value: `true`

If enabled, pileline will start to listen to the specified address and port serve a web interface and web api.


### webInterfacePort
`webInterfacePort=<integer>`

Default value: `8080`


### webVirtualDirectoryPath
`webVirtualDirectoryPath=<string>`

Default value: `/bigdatapipeline`


### webInterfaceDisplayOnBrowserOnStart
`webInterfaceDisplayOnBrowserOnStart=<boolean>`

Default value: `false`

If enabled, will try to open the browser with the web interface start page.


### webInterfacePort
`webInterfacePort=<integer>`

Default value: `8080`

