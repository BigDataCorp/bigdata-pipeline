bigdata-pipeline
================

###Latest Release
https://github.com/BigDataCorp/bigdata-pipeline/releases/tag/0.8

###Documentation
[Documentation](./docs/Home.md)

###Install
[Wiki Documentation on Install](../../wiki/Install)

### Main Features
1. Mono support
2. Refactored Module System
3. Plugable Web endpoints (Nancy controllers and views)

### Refactored Module System
The system has evolved to have plugins/modules for:

1. Authentication
2. Storage
3. Action
4. System
5. Nancy Controller and View

### Available Storage Modules
*Note: Only one storage module can be used. If more than one is installed, only the first one is loaded...*

1. MongoDb Storage: uses mongodb for storage (mono and windows)
2. SQLite Storage: windows only


### Notes on using MongoDb Storage
A connection string and database name must be configured in the app.config:
**Connection string format**
```
mongodb://[username:password@]host1[:port1][,host2[:port2],...[,hostN[:portN]]][/[database][?options]]
```

**Sample configuration:**
```
  <appSettings>
	<add key="mongoConnectionString" value="mongodb://username:password@localhost:27017/admin" />
	<add key="mongoDatabaseName" value="BigdataPipeline" />
  </appSettings>
```
