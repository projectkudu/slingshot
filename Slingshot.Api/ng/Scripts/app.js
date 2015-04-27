/*\
|*|
|*|  IE-specific polyfill which enables the passage of arbitrary arguments to the
|*|  callback functions of JavaScript timers (HTML5 standard syntax).
|*|
|*|  https://developer.mozilla.org/en-US/docs/DOM/window.setInterval
|*|
|*|  Syntax:
|*|  var timeoutID = window.setTimeout(func, delay, [param1, param2, ...]);
|*|  var timeoutID = window.setTimeout(code, delay);
|*|  var intervalID = window.setInterval(func, delay[, param1, param2, ...]);
|*|  var intervalID = window.setInterval(code, delay);
|*|
\*/

if (document.all && !window.setTimeout.isPolyfill) {
    var __nativeST__ = window.setTimeout;
    window.setTimeout = function (vCallback, nDelay /*, argumentToPass1, argumentToPass2, etc. */) {
        var aArgs = Array.prototype.slice.call(arguments, 2);
        return __nativeST__(vCallback instanceof Function ? function () {
            vCallback.apply(null, aArgs);
        } : vCallback, nDelay);
    };
    window.setTimeout.isPolyfill = true;
}

if (document.all && !window.setInterval.isPolyfill) {
    var __nativeSI__ = window.setInterval;
    window.setInterval = function (vCallback, nDelay /*, argumentToPass1, argumentToPass2, etc. */) {
        var aArgs = Array.prototype.slice.call(arguments, 2);
        return __nativeSI__(vCallback instanceof Function ? function () {
            vCallback.apply(null, aArgs);
        } : vCallback, nDelay);
    };
    window.setInterval.isPolyfill = true;
}

// First, checks if it isn't implemented yet.
if (!String.prototype.format) {
  String.prototype.format = function() {
    var args = arguments;
    return this.replace(/{(\d+)}/g, function(match, number) { 
      return typeof args[number] != 'undefined'
        ? args[number]
        : match
      ;
    });
  };
}

var telemetryObj = function(){
    var that = {};
    that.logGetTemplate = function(repoUrl){
        appInsights.trackEvent("GetTemplate", { repoUrl: repoUrl });
    }

    that.logDeploy = function(repoUrl){
        appInsights.trackEvent("Deploy", { repoUrl: repoUrl });
    }

    that.logDeploySucceeded = function(repoUrl){
        appInsights.trackEvent("DeploySucceeded", { repoUrl: repoUrl });
    }

    that.logDeployFailed = function(repoUrl){
        appInsights.trackEvent("DeployFailed", { repoUrl: repoUrl });
    }

    return that;
};

var contantsObj = function(){
    var that = {};
    var paramsObj = function(){
        var that = {};
        that.siteLocation = "siteLocation";
        that.siteLocationLower = that.siteLocation.toLowerCase();
        that.sqlServerLocation = "sqlServerLocation";
        that.sqlServerLocationLower = that.sqlServerLocation.toLowerCase();
        return that;
    }

    that.params = paramsObj();

    return that;
}

var telemetry = telemetryObj();
var constants = contantsObj();

(function () {

// app.js
// create our angular app and inject ngAnimate and ui-router 
// =============================================================================
angular.module('formApp', ['ngAnimate', 'ui.router'])

// configuring our routes 
// =============================================================================
.config(function($stateProvider, $urlRouterProvider) {
    
    $stateProvider
    
        // route to show our basic form (/form)
        .state('form', {
            url: '/form',
            templateUrl: 'ng/views/form.html',
            controller: 'FormController'
        })
        
        // nested states 
        // each of these sections will have their own view
        // url will be nested (/form/setup)
        .state('form.setup', {
            url: '/setup',
            templateUrl: 'ng/views/form-setup.html',
            controller: 'FormSetupController'

        })

        // /form/preview
        .state('form.preview', {
            url: '/preview',
            templateUrl: 'ng/views/form-preview.html',
            controller: 'FormPreviewController'

        })

        // /form/deploy
        .state('form.deploy', {
            url: '/deploy',
            templateUrl: 'ng/views/form-deploy.html',
            controller: 'FormDeployController'
        })

        // /form/infohome
        .state('form.infohome', {
            url: '/infohome',
            templateUrl: 'ng/views/form-infohome.html',
            controller: 'FormInfoHome'
        })

        // /form/infobutton
        .state('form.infobutton', {
            url: '/infobutton',
            templateUrl: 'ng/views/form-infobutton.html',
            controller: 'FormInfoButton'
        })

        // /form/infotemplates
        .state('form.infotemplates', {
            url: '/infotemplates',
            templateUrl: 'ng/views/form-infotemplates.html',
            controller: 'FormInfoTemplates'
        });

    // catch all route
    // send users to the form page 
    $urlRouterProvider.otherwise('/form/setup');
})

// Custom filters
// =============================================================================
.filter('camelCaseToHuman', function(){
    return function(input) {
        var camelCase = input.name.charAt(0).toUpperCase() + input.name.substr(1).replace(/[A-Z]/g, ' $&');
        if(input.defaultValue === ""){
            return camelCase + " (Optional)";
        }

        return camelCase;
    }
})

.controller('FormInfoHome', ['$scope', '$location', function($scope, $location) {}])
.controller('FormInfoButton', ['$scope', '$location', function($scope, $location) {}])
.controller('FormInfoTemplates', ['$scope', '$location', function($scope, $location) {}])

// our controller for the form
// =============================================================================
.controller('FormController', ['$scope', '$location', function($scope, $location) {
    
    // we will store all of our form data in this object
    $scope.formData = {};
    
    ////////////////////
    // Private Methods
    ////////////////////

    function initialize(){
        $scope.formData.repositoryUrl = getQueryVariable("repository");

        if(!$scope.formData.repositoryUrl || $scope.formData.repositoryUrl.length === 0){
            if(sessionStorage.repositoryUrl){
                $scope.formData.repositoryUrl = sessionStorage.repositoryUrl;
            }
            else{
                $location.url("/form/infohome");
                return;
            }
        }

        if($scope.formData.repositoryUrl){
            sessionStorage.repositoryUrl = $scope.formData.repositoryUrl;
            telemetry.logGetTemplate($scope.formData.repositoryUrl);
        }

        $location.url("/");
    }

    function getQueryVariable(variable) {
        var query = window.location.search.substring(1);
        var vars = query.split('&');
        for (var i = 0; i < vars.length; i++) {
            var pair = vars[i].split('=');
            if (decodeURIComponent(pair[0]) == variable) {
                return decodeURIComponent(pair[1]);
            }
        }

        return null;
    }

    initialize();
}])

.controller('FormSetupController', ['$scope', '$http', function($scope, $http){
    var paramObject = function(){
        var that = {};
        that.name = null;
        that.type = null;
        that.allowedValues = null;
        that.aliasValues = null;
        that.defaultValue = null;
        that.value = null;
        that.validationError = null;
        return that;
    };

    function initialize($scope, $http){
        // If we don't have the repository url, then don't init.  Also
        // if the user hit "back" from the next page, we don't re-init.
        if(!$scope.formData.repositoryUrl || $scope.formData.repositoryUrl.length === 0 || $scope.formData.subscriptions){
            return;
        }

        $http({
            method: "get",
            url: "api/template",
            params: {
                "repositoryUrl" : $scope.formData.repositoryUrl
            }
        })
        .then(function(result){
            $scope.formData.userDisplayName = result.data.userDisplayName;
            $scope.formData.template = result.data.template;
            $scope.formData.subscriptions = result.data.subscriptions;
            $scope.formData.siteLocations = result.data.siteLocations;
            $scope.formData.sqlServerLocations = result.data.sqlServerLocations;
            $scope.formData.templateUrl = result.data.templateUrl;
            $scope.formData.branch = result.data.branch;
            $scope.formData.tenants = result.data.tenants;
            $scope.formData.repositoryUrl = result.data.repositoryUrl;
            $scope.formData.repositoryDisplayUrl = result.data.repositoryDisplayUrl;
            $scope.formData.siteName = result.data.siteName;
            $scope.formData.siteNameQuery = result.data.siteName;
            $scope.formData.newResourceGroup = {
                name: result.data.resourceGroupName,
                location: ""
            };

            // Select current tenant
            var tenants = $scope.formData.tenants;
            for(var i = 0; i < tenants.length; i++){
                if(tenants[i].Current){
                    $scope.formData.tenant = tenants[i];
                }
            }

            // Select first subscription
            if($scope.formData.subscriptions && $scope.formData.subscriptions.length > 0){
                var sub = $scope.formData.subscriptions[0];
                $scope.formData.subscription = sub;
                setDefaultRg(sub);
            }

            // Pull out EULA metadata if available. Require http* and sanitize
            $scope.formData.eula = null;
            var metadata = result.data.template.metadata;
            if (metadata && metadata["eula"]) {
                var eula = encodeURI(metadata["eula"].trim());
                if (eula.indexOf('http') === 0) {
                    $scope.formData.eula = eula;
                }
            }

            // Pull out template parameters to show on UI
            $scope.formData.params = [];
            $scope.formData.repoParamFound = false;
            var parameters = $scope.formData.template.parameters;
            for(var name in parameters){
                var parameter = parameters[name];
                var param = paramObject();
                
                param.name = name;
                param.type = parameter.type;
                param.allowedValues = parameter.allowedValues;
                param.defaultValue = parameter.defaultValue;

                $scope.formData.params.push(param);

                var paramName = param.name.toLowerCase();
                if(paramName === "repourl"){
                    $scope.formData.repoParamFound = true;
                }
                else if(paramName === "sitename" && result.data.siteName){
                    param.value = result.data.siteName;
                    $scope.formData.siteNameAvailable = true;
                }
                else if(paramName === constants.params.siteLocationLower && $scope.formData.siteLocations && $scope.formData.siteLocations.length > 0 && !param.defaultValue){
                    param.value = $scope.formData.siteLocations[0];
                }
                else if(paramName === constants.params.sqlServerLocationLower && $scope.formData.sqlServerLocations && $scope.formData.sqlServerLocations.length > 0 && !param.defaultValue){
                    param.value =   $scope.formData.sqlServerLocations[0];
                }
                else if(paramName === "sqlservername" && $scope.formData.siteName && $scope.formData.siteName.length > 0 && !param.defaultValue){
                    var trimBeginExp = /^[^a-z0-9]+/;
                    var trimEndExp = /[^a-z0-9]+$/;
                    var badCharsExp = /[^a-z0-9\-]+/g;
                    var serverName = $scope.formData.siteName.toLowerCase();

                    serverName = serverName.replace(trimBeginExp, "");
                    serverName = serverName.replace(trimEndExp, "");
                    serverName = serverName.replace(badCharsExp, "");

                    param.value = serverName + "-server";
                }
                else if(paramName === "sqladministratorlogin" && $scope.formData.userDisplayName && $scope.formData.userDisplayName.length > 0 && !param.defaultValue){
                    param.value = $scope.formData.userDisplayName.toLowerCase().replace(/ /g, "");
                }

                if(!param.value){
                    param.value = param.defaultValue;
                }
            }
        },
        function(result){
            if(result.data){
                alert(result.data.error)
            }
        });
    }

    function setDefaultRg(sub){
        var curRg = null;

        var rgs = sub.resourceGroups;
        if(rgs.length === 0 || (rgs.length > 0 && rgs[0].location)){
            curRg = {
                name: "Create New",
                location: ""
            };

            sub.resourceGroups.unshift(curRg);
        }
        else{
            curRg = rgs[0];
        }

        $scope.formData.existingResourceGroup = curRg;
    }

    function creatingNewRg(){
        return !$scope.formData.existingResourceGroup.location;
    }

    $scope.changeTenant = function(){
        var tenantUrl = window.location.origin + window.location.pathname + "api/tenants/" + $scope.formData.tenant.TenantId;
        window.location = tenantUrl;
    }

    $scope.changeSubscription = function(){
        setDefaultRg($scope.formData.subscription);
    }

    $scope.changeResourceGroup = function(){
        if(creatingNewRg()){
            return;
        }

        $scope.formData.params.forEach(function(param){
            var name = param.name.toLowerCase();
            var locations = null;

            if(name === constants.params.siteLocationLower){
                locations = $scope.formData.siteLocations;
            }
            else if(name === constants.params.sqlServerLocationLower){
                locations = $scope.formData.sqlServerLocations;
            }

            if(locations){
                for(var i = 0; i < locations.length; i++){
                    // Site/SQL locations have spaces in them
                    if(locations[i].replace(/ /g, "").toLowerCase() === $scope.formData.existingResourceGroup.location){
                        param.value = locations[i];
                        break;
                    }
                }
            }
        });
    }

    $scope.showParam = function(param){
        var name = param.name.toLowerCase();
        if(name === 'repourl' && $scope.formData.repositoryUrl){
            param.value = $scope.formData.repositoryUrl;
            return false;
        }
        else if(name === 'branch'){
            param.value = $scope.formData.branch;
            return false;
        }
        else if(name === 'hostingplanname'){
            return false;
        }
        else if(name === 'workersize'){
            if(!param.aliased){
                param.aliased = true;

                // Creating aliases this way means that we need to undo them later.  There should be a better
                // way to get this to work with Angular's select box, but I couldn't get it to work so
                // for now this will have to do.
                param.allowedValues[0] = "Small";
                param.allowedValues[1] = "Medium";
                param.allowedValues[2] = "Large";
            }

            var skuParam = getParamByName($scope.formData.params, 'sku');
            if(skuParam &&
                (skuParam.value === 'Free' ||
                skuParam.value === 'Shared')){
                    param.value = 'Small';
                    return false;
            }
        }

        return true;
    }

    $scope.enableTooltip = function(id){
        var $tooltip = $(id);
        $tooltip.tooltip();
    }

    function getParamByName(params, name){
        for(var i = 0; i < params.length; i++){
            if(params[i].name.toLowerCase() === name.toLowerCase()){
                return params[i];
            }
        }

        return null;
    }

    $scope.getFieldType = function(param){
        if(param.name.toLowerCase().indexOf("password") >= 0){
            return "password";
        }
        if(param.allowedValues){
            return "select";
        }
        else{
            return "text";
        }
    }

    $scope.canMoveToNextStep = function(){
        var isValid = true;

        if (!$scope.formData.tenant
            || !$scope.formData.subscription
            || !$scope.formData.params) {

            return false;
        }

        var rgs = $scope.formData.subscription.resourceGroups;
        if(creatingNewRg()){

            var regex = new RegExp("^[0-9a-zA-Z\\()._-]+[0-9a-zA-Z()_-]$");
            if(!regex.test($scope.formData.newResourceGroup.name)){
                $scope.formData.resourceGroupError = "Invalid Resource Group Name";
                isValid = false;
            }
            else{
                for(var i = 0; i < rgs.length; i++){
                    if($scope.formData.newResourceGroup.name.toLowerCase() === rgs[i].name.toLowerCase()){
                        
                        $scope.formData.resourceGroupError = "Resource Group Exists";
                        isValid = false;
                    }
                }
            }

            if(isValid){
                $scope.formData.resourceGroupError = null;
            }
        }
        else{
            $scope.formData.resourceGroupError = null;
        }

        // If we're dealing with a site, and the name is not available, we can't go to next step.
        // In the case of non-site template, siteName will be undefined.
        if ($scope.formData.siteName === "" ||
            ($scope.formData.siteName &&
                (!$scope.formData.siteNameAvailable || $scope.formData.siteName != $scope.formData.siteNameQuery))) {
            isValid = false;
        }

        // Go through all the params, making sure none are blank
        var params = $scope.formData.params;
        for(var i = 0; i < params.length; i++){
            var param = params[i];

            if(param.name === 'hostingPlanName' && $scope.formData.siteName){
                param.value = $scope.formData.siteName;
            }

            if(param.type.toLowerCase() === 'int'){
                if(param.value && isNaN(param.value)){
                    param.validationError = "Must be a number";
                    isValid = false;
                }
                else{
                    param.validationError = null;
                    if(!param.value){
                        isValid = false;
                    }
                }
            }

            if(param.value === null || param.value === undefined){
                isValid = false;
            }
            else if(param.value === "" && param.defaultValue !== ""){
                isValid = false;
            }
        }

        return isValid;
    }

    $scope.checkSiteName = function(siteName){
        $scope.formData.siteNameAvailable = false;
        if(siteName){
            $scope.formData.siteNameQuery = siteName;
            window.setTimeout(querySiteName, 250, $scope, $http, siteName);
        }
        else{
            $scope.formData.siteName = '';
        }
    }

    $scope.showSiteNameAvailableMesg = function(){
        if(($scope.formData.siteName && $scope.formData.siteName.length > 0) &&
           ($scope.formData.siteNameQuery && $scope.formData.siteNameQuery.length > 0)){
            return true;
        }

        return false;
    }

    $scope.nextStep = function(){
        $scope.formData.deployPayload = getDeployPayload($scope.formData.params);
    }

    function querySiteName($scope, $http, siteName){
        // Check to make sure we still have the correct site name to query after the delay
        if($scope.formData.siteNameQuery === siteName){
            var subscriptionId = $scope.formData.subscriptions[0].subscriptionId
            $http({
                method: "get",
                url: "api/subscriptions/"+subscriptionId+"/sites/"+siteName
            })
            .then(function(result){
                // After getting the result, double check to make sure that the
                // sitename we queried still matches what the user has typed in
                if(result.data.siteName === $scope.formData.siteNameQuery){
                    $scope.formData.siteNameAvailable = result.data.isAvailable;
                    $scope.formData.siteName = result.data.siteName;
                }
            },function(result){
                if(result.data){
                    alert(result.data.error);
                }
            });
        }
    }

    function getDeployPayload(params){
        var dataParams = {}
        var rg = creatingNewRg() ? $scope.formData.newResourceGroup : $scope.formData.existingResourceGroup;
        $scope.formData.finalResourceGroup = rg;

        for(var i = 0; i < params.length; i++){
            var param = params[i];

            // Since we tranformed workersize to pretty values earlier, we need to convert them back
            if(param.name.toLowerCase() === "workersize"){
                param.value = param.allowedValues.indexOf(param.value).toString();
            }

            // JavaScript may convert string representations of numbers incorrectly
            if(typeof param.value === "number" && param.type.toLowerCase() === 'string'){
                param.value = param.value.toString();
            }
            else if(typeof param.value === "string" && param.type.toLowerCase() === "int"){
                param.value = parseInt(param.value);
            }

            if(creatingNewRg() &&
                (param.name.toLowerCase() === constants.params.siteLocationLower ||
                 param.name.toLowerCase() === constants.params.sqlServerLocationLower)){

                rg.location = param.value;
            }

            dataParams[param.name] = {value : param.value};
        }

        if(!rg.location){
            rg.location = "East US";
        }

        return {
            parameters: dataParams,
            subscriptionId: $scope.formData.subscription.subscriptionId,
            resourceGroup: rg,
            templateUrl: $scope.formData.templateUrl
        };
    }

    initialize($scope, $http);

}]) // end FormSetupController

.controller('FormPreviewController', ['$scope', '$http', function($scope, $http){
    function initialize($scope, $http){
        var subscriptionId = $scope.formData.subscription.subscriptionId;
        $scope.formData.providers = [];

        $http({
            method: "post",
            url: "api/preview/"+subscriptionId,
            data: $scope.formData.deployPayload
        })
        .then(function(result){
            $scope.formData.providers = result.data.providers;
        },
        function(result){
            if(result.data){
                alert(result.data.error);
            }
        });
    }

    initialize($scope, $http);
}]) // end FormPreviewController

.controller('FormDeployController', ['$scope', '$http', function($scope, $http){
    var statusMap = {};
    statusMap["microsoft.web/sites"] = "Creating Website";
    statusMap["microsoft.web/sites/config"] = "Updating Website Config";
    statusMap["microsoft.web/sites/sourcecontrols"] = "Setting up Source Control";
    statusMap["microsoft.web/serverfarms"] = "Creating Web Hosting Plan";
    statusMap["microsoft.insights/alertrules"] = "Adding Insights Alerts";
    statusMap["microsoft.insights/autoscalesettings"] = "Configuring Insights Auto Scale Settings";
    statusMap["microsoft.insights/components"] = "Configuring Insights Components";
    statusMap["microsoft.sql/servers"] = "Adding SQL Server";
    statusMap["microsoft.sql/servers/firewallrules"] = "Configuring SQL Server Firewall Rules";
    statusMap["microsoft.sql/servers/databases"] = "Adding SQL Server Database";

    var portalWebSiteFormat = "https://portal.azure.com#resource/subscriptions/{0}/resourceGroups/{1}/providers/Microsoft.Web/sites/{2}";
    var portalRGFormat = "https://portal.azure.com/#asset/HubsExtension/ResourceGroups//subscriptions/{0}/resourceGroups/{1}";

    $scope.showError = function(){
        $('#errorModal').modal('show');
    }

    $scope.retryDeploy = function(){
        initialize($scope, $http);
    }

    function initialize($scope, $http){
        var subscriptionId = $scope.formData.subscription.subscriptionId;
        $scope.formData.deploymentSucceeded = false;
        $scope.formData.errorMesg = null;
        $scope.formData.statusMesgs = [];
        telemetry.logDeploy($scope.formData.repositoryUrl);

        $scope.formData.statusMesgs.push("Submitting Deployment");
        $http({
            method: "post",
            url: "api/deployments/"+subscriptionId,
            data: $scope.formData.deployPayload
        })
        .then(function(result){
            $scope.formData.statusMesgs.push("Deployment Started");
            window.setTimeout(getStatus, 1000, $scope, $http);
        },
        function(result){
            $scope.formData.errorMesg = result.data.error;
        });
    }

    function getStatus($scope, $http, deploymentUrl){
        var subscriptionId = $scope.formData.subscription.subscriptionId;
        var resourceGroup =  $scope.formData.finalResourceGroup;

        var params;
        if ($scope.formData.repoParamFound) {
            params = {
                "siteName": $scope.formData.siteName,
            };
        }

        $http({
            method: "get",
            url: "api/deployments/" + subscriptionId + "/rg/" + resourceGroup.name,
            params: params,
        })
        .then(function(result){
            addStatusMesg($scope, result);

            // In some cases, errors will be hidden within the operations object.
            var ops = result.data.operations;
            var error = null;
            for(var i=0; i<ops.value.length; i++){
                var opProperties = ops.value[i].properties;
                if(opProperties.statusMessage &&
                   opProperties.statusMessage.error){
                    error = opProperties.statusMessage.error.message;
                }
                else if(opProperties.provisioningState === "Failed" &&
                    opProperties.statusMessage &&
                    opProperties.statusMessage.message){
                        error = opProperties.statusMessage.message;
                }
            }

            if(error){
                $scope.formData.errorMesg = error;
            }
            else if(result.data.provisioningState === "Failed"){
                addErrorMesg($scope, result);
            }
            else if(result.data.provisioningState === "Succeeded"){
                $scope.formData.siteUrl = result.data.siteUrl;

                if ($scope.formData.repoParamFound){
                    $scope.formData.portalUrl = portalWebSiteFormat.format(
                        $scope.formData.subscription.subscriptionId,
                        $scope.formData.finalResourceGroup.name,
                        $scope.formData.siteName);

                    window.setTimeout(getGitStatus, 1000, $scope, $http);
                }
                else {
                    $scope.formData.deploymentSucceeded = true;
                    $scope.formData.portalUrl =  portalRGFormat.format(
                        $scope.formData.subscription.subscriptionId,
                        $scope.formData.finalResourceGroup.name);
                    telemetry.logDeploySucceeded($scope.formData.repositoryUrl);
                }

            }
            else{
                window.setTimeout(getStatus, 1000, $scope, $http);
            }
        },
        
        function(result){
            $scope.formData.errorMesg = result.data.error;
        });
    }

    function addStatusMesg($scope, result){
        var ops = result.data.operations.value;
        for(var i=ops.length-1; i>=0; i--){
            var mesg = ops[i].properties.targetResource.resourceType;
            var key = mesg.toLowerCase();
            if(statusMap[key]){
                mesg = statusMap[key];
            }
            else{
                mesg = "Updating " + mesg;
            }

            if($scope.formData.statusMesgs.indexOf(mesg) < 0){
                $scope.formData.statusMesgs.push(mesg);
            }
        }
    }

    function addErrorMesg($scope, result){
        var ops = result.data.operations.value;
        var mesg = null;
        for(var i=0; i<ops.length; i++){
            if(ops[i].properties.provisioningState === "Failed"){
                mesg = ops[i].properties.statusMessage.Message;
            }
        }

        if(!mesg){
            mesg = "Failed Deployment";
        }

        telemetry.logDeployFailed($scope.formData.repositoryUrl);
        $scope.formData.errorMesg = mesg;
    }

    function getGitStatus($scope, $http){
        var subscriptionId = $scope.formData.subscription.subscriptionId;
        var siteName = $scope.formData.siteName;
        var resourceGroup = $scope.formData.finalResourceGroup.name;

        $http({
            method: "get",
            url: "api/deployments/" + subscriptionId + "/rg/" + resourceGroup + "/git",
            params: { siteName: siteName },
        })
        .then(function(result){
            var formData = $scope.formData;
            if(result.data.status === 4){
                formData.deploymentSucceeded = true;
                telemetry.logDeploySucceeded(formData.repositoryUrl);
            }
            else if(result.data.status === 3){
                formData.errorMesg = "Git deployment failed";
                telemetry.logDeployFailed(formData.repositoryUrl);
            }
            else{
                if(formData.statusMesgs[formData.statusMesgs.length-1] !== result.data.progress){
                    formData.statusMesgs.push(result.data.progress);
                }
                window.setTimeout(getGitStatus, 1000, $scope, $http);
            }
        },
        function(result){
            $scope.formData.errorMesg = result.data.error;
        });
    }

    initialize($scope, $http);

}]);  // end FormDeployController

})();
