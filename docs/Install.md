## Install
To install BigDataPipeline as a windows service (linux is not supported yet!), simply call the BigDataPipeline.exe passing the parameter "install"

`
BigDataPipeline.exe install
`

For reference see: http://topshelf.readthedocs.org/en/latest/overview/commandline.html

## Uninstall
`
BigDataPipeline.exe uninstall
`

## Start service (if not already running)
The service will be installed but will not be running. To start it, you can go to "services" or use the parameter "start".

`
BigDataPipeline.exe start
`

## Stop service

`
BigDataPipeline.exe stop
`

## Stand alone execution

BigDataPipeline can also be run as a process by calling:
`
BigDataPipeline.exe
`
