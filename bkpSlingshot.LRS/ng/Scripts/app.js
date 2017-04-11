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
    String.prototype.format = function () {
        var args = arguments;
        return this.replace(/{(\d+)}/g, function (match, number) {
            return typeof args[number] !== 'undefined'
              ? args[number]
              : match
            ;
        });
    };
}

var telemetryObj = function () {
    var that = {};
    that.logGetTemplate = function (repoUrl) {
        appInsights.trackEvent("GetTemplate", { repoUrl: repoUrl });

    }

    that.logDeploy = function (repoUrl) {
        appInsights.trackEvent("Deploy", { repoUrl: repoUrl });


    }
        
    that.logDeploySucceeded = function (repoUrl) {
        appInsights.trackEvent("DeploySucceeded", { repoUrl: repoUrl });
    }

    that.logDeployFailed = function (repoUrl) {
        appInsights.trackEvent("DeployFailed", { repoUrl: repoUrl });
    }

    return that;
};

var constantsObj = function () {
    var that = {};
    var paramsObj = function () {
        var that = {};
        that.pollingInterval = 3000;
        that.appServiceLocationLower = "appservicelocation";
        return that;
    }

    that.params = paramsObj();

    return that;
}

var telemetry = telemetryObj();
var constants = constantsObj();

function IsLocationParam(paramName) {
    paramName = paramName.toLowerCase();

    return paramName === constants.params.appServiceLocationLower ;
}

(function () {

    // app.js
    // create our angular app and inject ngAnimate and ui-router 
    // =============================================================================
    angular.module('formApp', ['ngAnimate', 'ui.router'])

    // configuring our routes 
    // =============================================================================
    .config(function ($stateProvider, $urlRouterProvider) {

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

        // send users to the form page 
        $urlRouterProvider.otherwise('/form/setup');
    })

    // Custom filters
    // =============================================================================
    .filter('camelCaseToHuman', function () {
        return function (input) {
            var camelCase = input.name.charAt(0).toUpperCase() + input.name.substr(1).replace(/[A-Z]/g, ' $&');
            if (input.defaultValue === "") {
                return camelCase + " (Optional)";
            }

            return camelCase;
        }
    })


    // our controller for the form
    // =============================================================================
    .controller('FormController', ['$scope', '$location', function ($scope, $location) {

        // we will store all of our form data in this object
        $scope.formData = {};

        ////////////////////
        // Private Methods
        ////////////////////

        function initialize() {
            $scope.formData.templateName = getQueryVariable("templateName");

            if (!$scope.formData.templateName || $scope.formData.templateName.length === 0) {
                if (sessionStorage.templateName) {
                    $scope.formData.templateName = sessionStorage.templateName;
                }
                else {
                    $location.url("https://azuredeploy.net");
                    return;
                }
            }

            if ($scope.formData.templateName) {
                sessionStorage.templateName = $scope.formData.templateName;
                telemetry.logGetTemplate($scope.formData.templateName);
            }
            $location.url("/");
        }

        function getQueryVariable(variable) {
            var query = window.location.search,
                token = variable + "=",
                startIndex = query.indexOf(token);

            if (startIndex >= 0) {
                return query.substring(startIndex + token.length);
            }
            return null;
        }

        initialize();
    }])

    .controller('FormSetupController', ['$window','$scope', '$http', function ($scope, $http) {
        var paramObject = function () {
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
        var statusMap = {};
        statusMap["microsoft.web/sites"] = "Creating Website";
        statusMap["microsoft.web/sites/config"] = "Updating Website Config";
        statusMap["microsoft.web/sites/sourcecontrols"] = "Setting up Source Control";
        statusMap["microsoft.web/serverfarms"] = "Creating Web Hosting Plan";
        var portalWebSiteFormat = "https://portal.azure.com#resource/subscriptions/{0}/resourceGroups/{1}/providers/Microsoft.Web/sites/{2}/quickstart";
        var portalRGFormat = "https://portal.azure.com/#asset/HubsExtension/ResourceGroups//subscriptions/{0}/resourceGroups/{1}";

        $scope.showError = function () {
            $('#errorModal').toggle();
        }
        $scope.retryDeploy = function () {
            deploy();
        }

        function initialize($scope, $http) {
            // If we don't have the repository url, then don't init.  Also
            // if the user hit "back" from the next page, we don't re-init.
            if (!$scope.formData.templateName || $scope.formData.templateName.length === 0 || $scope.formData.subscriptions) {
                return;
            }

            $http({
                method: "get",
                url: "api/template",
                params: {
                    "templateName": $scope.formData.templateName
                }
            })
            .then(function (result) {
                $scope.formData.userDisplayName = result.data.userDisplayName;
                $scope.formData.subscriptions = result.data.subscriptions;
                $scope.formData.tenants = result.data.tenants;
                $scope.formData.branch = result.data.branch;
                $scope.formData.template = result.data.template;
                $scope.formData.templateName = result.data.templateName;
                $scope.formData.templateName = result.data.templateName;
                $scope.formData.appServiceLocations = result.data.appServiceLocations;
                $scope.formData.appServiceName = result.data.appServiceName;
                $scope.formData.appServiceNameQuery = result.data.appServiceName;
                $scope.formData.templateNameUrl = result.data.templateUrl;
                $scope.formData.repositoryDisplayUrl = result.data.repositoryDisplayUrl;
                $scope.formData.scmType = result.data.scmType;
                $scope.formData.newResourceGroup = {
                    name: result.data.resourceGroupName,
                    location: ""
                };

                // Select first subscription
                if ($scope.formData.subscriptions && $scope.formData.subscriptions.length > 0) {
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
                for (var name in parameters) {
                    var parameter = parameters[name];
                    var param = paramObject();

                    param.name = name;
                    param.type = parameter.type;
                    param.allowedValues = parameter.allowedValues;
                    param.defaultValue = parameter.defaultValue;
                    param.defaultValueComeFirst = parameter.defaultValueComeFirst;

                    $scope.formData.params.push(param);

                    var paramName = param.name.toLowerCase();
                    if (paramName === "msdeploypackageurl") {
                        $scope.formData.repoParamFound = true;
                    }
                    else if (paramName === "appservicename") {
                        param.value = result.data.appServiceName;
                        $scope.formData.appServiceNameAvailable = true;

                        if (param.defaultValueComeFirst) {
                            $scope.formData.appServiceName = param.defaultValue;
                            $scope.formData.appServiceNameQuery = param.defaultValue;
                        }
                    }
                    if (param.value === null || typeof param.value === "undefined" || param.defaultValueComeFirst) {
                        param.value = param.defaultValue;
                    }
                }
                deploy();
            },
            function (result) {
                if (result.data) {
                    $scope.formData.errorMesg = result.data.error;
                }
            });
        }

        function deploy() {
            var subscriptionId = $scope.formData.subscription.subscriptionId;
            $scope.formData.deploymentSucceeded = false;
            $scope.formData.errorMesg = null;
            $scope.formData.statusMesgs = [];
            telemetry.logDeploy($scope.formData.templateName);
            $scope.formData.deployPayload = getDeployPayload($scope.formData.params);

            $scope.formData.statusMesgs.push("Submitting Deployment");
            $http({
                method: "post",
                url: "api/deployments/" + subscriptionId,
                data: $scope.formData.deployPayload
            })
            .then(function (result) {
                $scope.formData.statusMesgs.push("Deployment Started");
                window.setTimeout(getStatus, constants.params.pollingInterval, $scope, $http);
            },
            function (result) {
                $scope.formData.errorMesg = result.data.error;
            });
        }

        function setDefaultRg(sub) {
            var curRg = curRg = {
                    name: "Create New",
                    location: ""
                };
            $scope.formData.existingResourceGroup = curRg;
        }
        $scope.changeSubscription = function () {
            setDefaultRg($scope.formData.subscription);
        }


        $scope.showParam = function (param) {
            var name = param.name.toLowerCase();
            if (name === 'templatename' && $scope.formData.templateName) {
                param.value = $scope.formData.templateName;
                return false;
            }
            else if (name === 'branch') {
                param.value = $scope.formData.branch;
                return false;
            }
            else if (name === 'hostingplanname') {
                return false;
            }
            else if (name === 'ismercurial') {
                return false;
            }
            else if (name === "ismanualintegration") {
                return $scope.formData.scmProvider === "Bitbucket";
            }
            else if (name === 'workersize') {
                if (!param.aliased) {
                    param.aliased = true;

                    // Creating aliases this way means that we need to undo them later.  There should be a better
                    // way to get this to work with Angular's select box, but I couldn't get it to work so
                    // for now this will have to do.
                    param.allowedValues[0] = "Small";
                    param.allowedValues[1] = "Medium";
                    param.allowedValues[2] = "Large";
                }

                var skuParam = getParamByName($scope.formData.params, 'sku');
                if (skuParam &&
                    (skuParam.value === 'Free' ||
                    skuParam.value === 'Shared')) {
                    param.value = 'Small';
                    return false;
                }
            }

            return true;
        }

        $scope.enableTooltip = function (id) {
            var $tooltip = $(id);
            $tooltip.tooltip();
        }

        function getParamByName(params, name) {
            for (var i = 0; i < params.length; i++) {
                if (params[i].name.toLowerCase() === name.toLowerCase()) {
                    return params[i];
                }
            }

            return null;
        }

        $scope.getFieldType = function (param) {
            if (param.name.toLowerCase().indexOf("password") >= 0) {
                return "password";
            }
            if (param.allowedValues) {
                return "select";
            }
            else {
                return "text";
            }
        }

        $scope.canMoveToNextStep = function () {
            var isValid = true;

            if (!$scope.formData.subscription
                || !$scope.formData.params) {

                return false;
            }

            var rgs = $scope.formData.subscription.resourceGroups;
                var regex = new RegExp("^[0-9a-zA-Z\\()._-]+[0-9a-zA-Z()_-]$");
                if (!regex.test($scope.formData.newResourceGroup.name)) {
                    $scope.formData.resourceGroupError = "Invalid Resource Group Name";
                    isValid = false;

                if (isValid) {
                    $scope.formData.resourceGroupError = null;
                }
            }
            else {
                $scope.formData.resourceGroupError = null;
            }

            // If we're dealing with a site, and the name is not available, we can't go to next step.
            // In the case of non-site template, appServiceName will be undefined.
                if ($scope.formData.appServiceName === "" ||
                ($scope.formData.appServiceName &&
                    (!$scope.formData.appServiceNameAvailable || $scope.formData.appServiceName !== $scope.formData.appServiceNameQuery))) {
                isValid = false;
            }

            // Go through all the params, making sure none are blank
            var params = $scope.formData.params;
            for (var i = 0; i < params.length; i++) {
                var param = params[i];

                if (param.type.toLowerCase() === 'int') {
                    if (param.value && isNaN(param.value)) {
                        param.validationError = "Must be a number";
                        isValid = false;
                    }
                    else {
                        param.validationError = null;
                        if (!param.value) {
                            isValid = false;
                        }
                    }
                }

                if (param.value === null || param.value === undefined) {
                    isValid = false;
                }
                else if (param.value === "" && param.defaultValue !== "") {
                    isValid = false;
                }
            }

            return isValid;
        }

        $scope.checkAppServiceName = function (appServiceName) {
            $scope.formData.appServiceNameAvailable = false;
            if (appServiceName) {
                $scope.formData.appServiceNameQuery = appServiceName;
                window.setTimeout(queryAppServiceName, 250, $scope, $http, appServiceName);
            }
            else {
                $scope.formData.appServiceName = '';
            }
        }

        $scope.showAppServiceNameAvailableMesg = function () {
            if (($scope.formData.appServiceName && $scope.formData.appServiceName.length > 0))  {
                return true;
            }

            return false;
        }

        function queryAppServiceName($scope, $http, appServiceName) {
            // Check to make sure we still have the correct site name to query after the delay
            if ($scope.formData.appServiceNameQuery === appServiceName) {
                var subscriptionId = $scope.formData.subscriptions[0].subscriptionId
                $http({
                    method: "get",
                    url: "api/subscriptions/" + subscriptionId + "/sites/" + appServiceName
                })
                .then(function (result) {
                    // After getting the result, double check to make sure that the
                    // sitename we queried still matches what the user has typed in
                    if (result.data.appServiceName === $scope.formData.appServiceNameQuery) {
                        $scope.formData.appServiceNameAvailable = result.data.isAvailable;
                        $scope.formData.appServiceName = result.data.appServiceName;
                        $scope.formData.deployPayload = getDeployPayload($scope.formData.params);
                    }
                }, function (result) {
                    if (result.data) {
                        alert(result.data.error);
                    }
                });
            }
        }

        function getDeployPayload(params) {
            var dataParams = {}
            var rg = $scope.formData.newResourceGroup ;
            $scope.formData.finalResourceGroup = rg;

            for (var i = 0; i < params.length; i++) {
                var param = params[i];

                // Since we tranformed workersize to pretty values earlier, we need to convert them back
                if (param.name.toLowerCase() === "workersize") {
                    param.value = param.allowedValues.indexOf(param.value).toString();
                }

                // JavaScript may convert string representations of numbers incorrectly
                if (typeof param.value === "number" && param.type.toLowerCase() === 'string') {
                    param.value = param.value.toString();
                }
                else if (typeof param.value === "string" && param.type.toLowerCase() === "int") {
                    param.value = parseInt(param.value);
                }

                if ((IsLocationParam(param.name))){
                    var location = $scope.formData.appServiceLocations[~~(Math.random() * $scope.formData.appServiceLocations.length)];
                    param.value = location;
                    rg.location = location;
                }

                dataParams[param.name] = { value: param.value };
            }


            return {
                parameters: dataParams,
                subscriptionId: $scope.formData.subscription.subscriptionId,
                resourceGroup: rg,
                templateUrl: $scope.formData.templateNameUrl,
                repoUrl: sessionStorage.templateName
            };
        }

        function getStatus($scope, $http, deploymentUrl) {
            var subscriptionId = $scope.formData.subscription.subscriptionId;
            var resourceGroup = $scope.formData.finalResourceGroup;

            var params;
            if ($scope.formData.repoParamFound) {
                params = {
                    "appServiceName": $scope.formData.appServiceName,
                };
            }

            $http({
                method: "get",
                url: "api/deployments/" + subscriptionId + "/rg/" + resourceGroup.name,
                params: params,
            })
            .then(function (result) {
                addStatusMesg($scope, result);

                // In some cases, errors will be hidden within the operations object.
                var ops = result.data.operations;
                var error = null;
                for (var i = 0; i < ops.value.length; i++) {
                    var opProperties = ops.value[i].properties;
                    if (opProperties.statusMessage &&
                       opProperties.statusMessage.error) {
                        error = opProperties.statusMessage.error.message;
                    }
                    else if (opProperties.provisioningState === "Failed" &&
                        opProperties.statusMessage &&
                        opProperties.statusMessage.message) {
                        error = opProperties.statusMessage.message;
                    }
                }

                if (error) {
                    $scope.formData.errorMesg = error;
                }
                else if (result.data.provisioningState === "Failed") {
                    addErrorMesg($scope, result);
                }
                else if (result.data.provisioningState === "Succeeded") {
                    $scope.formData.siteUrl = result.data.siteUrl;

                    $scope.formData.deploymentSucceeded = true;
                    $scope.formData.portalUrl = portalRGFormat.format(
                        $scope.formData.subscription.subscriptionId,
                        $scope.formData.finalResourceGroup.name);
                    telemetry.logDeploySucceeded($scope.formData.templateName);
                    $window. $scope.formData.portalUrl
                }
                else {
                    window.setTimeout(getStatus, constants.params.pollingInterval, $scope, $http);
                }
            },

            function (result) {
                $scope.formData.errorMesg = result.data.error;
            });
        }

        function addStatusMesg($scope, result) {
            var ops = result.data.operations.value;
            for (var i = ops.length - 1; i >= 0; i--) {
                var mesg = ops[i].properties.targetResource.resourceType;
                var key = mesg.toLowerCase();
                if (statusMap[key]) {
                    mesg = statusMap[key];
                }
                else {
                    mesg = "Updating " + mesg;
                }

                if ($scope.formData.statusMesgs.indexOf(mesg) < 0) {
                    $scope.formData.statusMesgs.push(mesg);
                }
            }
        }

        function addErrorMesg($scope, result) {
            var ops = result.data.operations.value;
            var mesg = null;
            for (var i = 0; i < ops.length; i++) {
                if (ops[i].properties.provisioningState === "Failed") {
                    mesg = ops[i].properties.statusMessage.Message;
                }
            }

            if (!mesg) {
                mesg = "Failed Deployment";
            }

            telemetry.logDeployFailed($scope.formData.templateName);
            $scope.formData.errorMesg = mesg;
        }
        initialize($scope, $http);

    }]) // end FormSetupController

})();
