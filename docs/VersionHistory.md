# Version History

## 0.9

1. PipelineCollection was removed

2. Better plugin/module loading

3. More interfaces:
    * IActionModule
    * ISystemModule
    * IStorageModule
    * IAccessControlModule
    * IActionLogOutput

4. Redesigned IActionModule 
    * SessionContext for each action execution.
    * SessionContext contains all parameters

5. Job refactored    
    * PluginId => Module
	* Comments => Description
    
6. Module refactored
    * IAWSRedshiftPluginDynamicScript => Initialize (DbConnection connection, AWSS3Helper s3, ISessionContext context)