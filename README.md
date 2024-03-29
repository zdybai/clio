# Introduction
Command Line Interface clio is the utility for integration Creatio platform with development and CI/CD tools.

With aid of clio you can:
- Maintanance Creatio packages
  - Create new packages in local file system
  - Push package from local file system to cloud application
  - Pull package from cloud application to local file system
  - Compress package to .gz file
- Maintanance Creatio application
  - Restart application
  - Clear session and cache storage (redisdb)
- Build CI/CD pipelines
- Convert existing Creatio package to project


# Installation and features

You can dowload release binaries from [latest release](https://github.com/Advance-Technologies-Foundation/clio/releases). Unpack the archive with clio.

# Content table
- [Introduction](#introduction)
- [Installation and features](#installation-and-features)
- [Content table](#content-table)
- [Arguments](#arguments)
- [Register](#register)
	- [Windows](#windows)
	- [MacOS](#macos)
	- [Help and examples](#help-and-examples)
- [Packages](#packages)
	- [Creating new package](#creating-new-package)
	- [Installing package](#installing-package)
	- [Pull package from remote application](#pull-package-from-remote-application)
	- [Delete package](#delete-package)
	- [Compress package](#compress-package)
- [Application](#application)
	- [Restart application](#restart-application)
	- [Clear redis database](#clear-redis-database)
- [Environment settings](#environment-settings)
	- [Create/Update an environment](#createupdate-an-environment)
	- [Delete the existing environment](#delete-the-existing-environment)
	- [View application options](#view-application-options)
- [Using for CI/CD systems](#using-for-cicd-systems)
- [Development](#development)
	- [Convert existing package to project](#convert-existing-package-to-project)
	- [Execute assembly](#execute-assembly)
	- [References](#references)
	- [Execute custom SQL script](#execute-custom-sql-script)

# Arguments
- `<PACKAGE_NAME>` - package name
- `<ENVIRONMENT_NAME>` - environment name
- `<COMMAND_NAME>` - clio command name

# Register


## Windows

To register clio as the global command, run the command in CLI directory:

```
dotnet clio.dll register
```
you can register clio for all users
```
dotnet clio.dll register -t m
```

## MacOS

1. Download [.net core](https://dotnet.microsoft.com/download/dotnet-core) for mac
2. Download and extract clio [release](https://github.com/Advance-Technologies-Foundation/clio/releases)
3. [Register](https://www.architectryan.com/2012/10/02/add-to-the-path-on-mac-os-x-mountain-lion/) clio folder in PATH system variables

In terminal execute command for check success register
```
clio help
```

## Help and examples

For display available commands use:
```
clio help
```
For display command help use:
```
clio <COMMAND_NAME> --help
```

# Packages

## Creating new package

To create new package project, use the next command:
```
 clio new-pkg <PACKAGE_NAME>
```
you can set reference on local core assembly with using Creatio file design mode with command in Pkg directory
```
 clio new-pkg <PACKAGE_NAME> -r bin
```

## Installing package

To install package from directory you can use the next command:
for non compressed package in current folder
```
clio push-pkg <PACKAGE_NAME>
```
or for .gz packages you can use command:
```
clio push-pkg package.gz
```
or with full path
```
clio push-pkg C:\Packages\package.gz
```
for get installation log file specify report path parameter
```
clio push-pkg <PACKAGE_NAME> -r log.txt
```

## Pull package from remote application

For download package to local file system from application use command:
```
clio pull-pkg <PACKAGE_NAME>
```
for pull package from non default application
```
clio pull-pkg <PACKAGE_NAME> -e <ENVIRONMENT_NAME>
```
Applies to Creatio 7.14.0 and up

## Delete package

To delete package, use the next command:
```
clio delete-pkg-remote <PACKAGE_NAME>
```
for delete for non default application
```
clio delete-pkg-remote <PACKAGE_NAME> -e <ENVIRONMENT_NAME>
```

## Compress package

For compress package into *.gz archive for directory which contain package folder
```
clio generate-pkg-zip <PACKAGE_NAME>
```
or you can specify full path for package and .gz file
```
clio generate-pkg-zip  C:\Packages\package -d C:\Store\package.gz
```

# Application

## Restart application

To restart Creatio application, use the next command for default environment:

```
clio restart-web-app
```
or for register application
```
clio restart-web-app <ENVIRONMENT_NAME>
```

## Clear redis database
For default application
```
clio clear-redis-db
```
or non default application
```
clio clear-redis-db <ENVIRONMENT_NAME>
```

# Environment settings

Environment is the set of configuration options. It consist of name, Creatio application URL, login and password.

## Create/Update an environment

Register new application settings

```
clio reg-web-app <ENVIRONMENT_NAME> -u http://mysite.creatio.com -l administrator -p password
```
or update existing settings
```
clio reg-web-app <ENVIRONMENT_NAME> -u administrator -p password
```

## Delete the existing environment

```
clio unreg-web-app <ENVIRONMENT_NAME>
```

## View application options

For view list of all applications
```
clio show-web-app-list
```
or for concrete application
```
clio show-web-app <ENVIRONMENT_NAME>
```

# Using for CI/CD systems
In CI/CD systems, you can specify configuration options directly when calling command:
```
clio restart -u http://mysite.creatio.com -l administrator -p password
```


# Development

## Convert existing package to project

Convert package with name MyApp and MyIntegration, located in directory C:\Pkg
```
clio convert <PACKAGE_NAME>
```

## Execute assembly

Execute code from assembly
```
clio execute-assembly-code -f myassembly.dll -t MyNamespace.CodeExecutor
```

## References

Set references for project on src
```
clio ref-to src
```
Set references for project on application distributive binary files
```
clio ref-to bin
```
## Execute custom SQL script

Execute custom SQL script on a web application
```
execute-sql-script "SELECT Id FROM SysSettings WHERE Code = 'CustomPackageId'"
```
Executes custom SQL script from specified file
```
execute-sql-script -f c:\Path to file\file.sql
```
