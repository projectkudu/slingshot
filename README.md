SlingShot
========
SlingShot is a service that enable one click deploy your web app from your repository to Azure App Service

All you need to do is place below markdown to your README.md file.
````
[![Deploy to Azure](https://azuredeploy.net/deploybutton.svg)](https://azuredeploy.net/)
````
or HTML if you like
````
<a href="https://azuredeploy.net/" target="_blank">
    <img src="https://azuredeploy.net/deploybutton.svg"/>
</a>
````

Advance configuration
============
Behind the scene, azuredeploy.net get the repository url from referal url and redirect request to deploy.azure.com. And some of the ***query strings from repository url*** will be honored. We can utilize these query strings for advance configuration.

Github (public repository only)
------------
 * Deploy with linking paramater (Only if there is a azuredeploy.json at the root of your repository)
  * Query string "ptmpl" : any public url pointing to parameter json file or relative path from current repository

````
[![Deploy to Azure](https://azuredeploy.net/deploybutton.svg)](https://deploy.azure.com/?repository={your repo url}?ptmpl={url to paramter json file or relative path from current repo})

e.g

[![Deploy to Azure](https://azuredeploy.net/deploybutton.svg)](https://deploy.azure.com/?repository=https://github.com/shrimpy/ArmParamterTemplateTest?ptmpl=https://raw.githubusercontent.com/shrimpy/ArmParamterTemplateTest/master/parameters.azuredeploy.json)

[![Deploy to Azure](https://azuredeploy.net/deploybutton.svg)](https://deploy.azure.com/?repository=https://github.com/shrimpy/ArmParamterTemplateTest?ptmpl=parameters.azuredeploy.json)
````

Bitbucket (public and private repository)
------------
 * Deploy with linking paramater (Only if there is a azuredeploy.json at the root of your repository)
  * Query string "ptmpl" : any public url pointing to parameter json file or relative path from current repository

````

[![Deploy to Azure](https://azuredeploy.net/deploybutton.svg)](https://deploy.azure.com/?repository={your repo url}?ptmpl={url to paramter json file or relative path from current repo})

e.g 

[![Deploy to Azure](https://azuredeploy.net/deploybutton.svg)](https://localhost:44300/?repository=https://bitbucket.org/shrimpywu/armparametertemplatetest?ptmpl=https://bitbucket.org/shrimpywu/armparametertemplatetest/raw/master/parameters.azuredeploy.json)

[![Deploy to Azure](https://azuredeploy.net/deploybutton.svg)](https://localhost:44300/?repository=https://bitbucket.org/shrimpywu/armparametertemplatetest?ptmpl=parameters.azuredeploy.json)
````

 * Deploy from a pull request
  * Query string "pr" : pull request id

````
[![Deploy to Azure](https://azuredeploy.net/deploybutton.svg)](https://localhost:44300/?repository={repository url}?pr={pull request id})

e.g

[![Deploy to Azure](https://azuredeploy.net/deploybutton.svg)](https://localhost:44300/?repository=https://bitbucket.org/shrimpywu/armparametertemplatetest?pr=1)
````

 * Enable continues deployment
  * Query string "manual" : true/false, default is false. Require single-sign-on with Azure.

````
[![Deploy to Azure](https://azuredeploy.net/deploybutton.svg)](https://localhost:44300/?repository={repository url}?manual={true/false})

e.g

[![Deploy to Azure](https://azuredeploy.net/deploybutton.svg)](https://localhost:44300/?repository=https://bitbucket.org/shrimpywu/armparametertemplatetest?manual=true)
````

- Combine query strings

````
[![Deploy to Azure](https://azuredeploy.net/deploybutton.svg)](https://localhost:44300/?repository=https://bitbucket.org/shrimpywu/armparametertemplatetest?ptmpl=https://bitbucket.org/shrimpywu/armparametertemplatetest/raw/master/parameters.azuredeploy.json&pr=1)
````

Instructions for how to run SlingShot locally
============
1. Clone this repository to your local drive.
2. Open `slingshot.sln` with VS 2012+ and compile.

Create AAD application
======================
1. Go to [Azure Portal](https://manage.windowsazure.com/) while logged in as an Org ID (i.e. not MSA) and create AAD Application. You may create an application on existing AAD directory or a new directory altogether.
1. Select `Add an application my organization is developing`
1. Enter any name for application name.
1. Select `WEB APPLICATION AND/OR WEB API`
1. Enter `https://localhost:44306/` as `SIGN ON URL` 
1. For `APP ID URL`, enter something like `https://davidebboslingshot.onmicrosoft.com/`.
1. Once created, click `CONFIGURE` tab
1. On `Permission to other applications`, add `Windows Azure Service Management API` and check `Access Azure Service Management` for `Delegated Permissions` and save.

Fix AADClientId and AADClientSecret in codes
============================================
1. Copy `CLIENT ID` and paste it in [this line](https://github.com/suwatch/ARMOAuth/blob/master/Modules/ARMOAuthModule.cs), replacing `Environment.GetEnvironmentVariable("AADClientId")`.
2. On `Keys` section, create a client secret. Copy the key and paste it in the same file, replacing `Environment.GetEnvironmentVariable("AADClientSecret")`.

Or as a cleaner alternative, you can set the `AADClientId` and `AADClientSecret` environment variables on your machine so that the code picks it up without having to modify it.


Test with localhost
===================
1. In VS, make Slingshot.Api the starter project. The RedirectionSite project can be ignored.
1. Starting running it in the debugger (F5).
1. In browser, it should redirect to login page.
1. Enter AAD account and password.
  Note: try account that is not in the same directly as the application.
  Note: currently this does not work with MSA account.
1. You should be prompt with OAuth allow/deny page, do accept it.

Test ARM apis
=============
1. `https://localhost:44306/api/token` - show current token details.
2. `https://localhost:44306/api/tenants` - show all tenants (AAD directory) user belongs to.
3. `https://localhost:44306/api/tenants/<tenant-id>` - to switch tenant.
4. `https://localhost:44306/api/subscriptions` - list subscriptions.
5. `https://localhost:44306/api/subscriptions/<sub-id>/resourceGroups` - list resourceGroups for a subscription.
6. `https://localhost:44306/api/subscriptions/<sub-id>/resourceGroups/<resource>/providers/Microsoft.Web/sites` - list sites.
7. and so on.. 

Test with Azure Websites
========================
1. Create Azure Websites with local git publishing enabled
2. Add the site https url as the reply URL for AAD application
3. Deploy the website by pushing the repository
4. Set AADClientID and AADClientSecret appSettings
5. To test, simply browse to the website and append the query string "?repository=<url of your Git repository>"

Any issue, do let me know.
