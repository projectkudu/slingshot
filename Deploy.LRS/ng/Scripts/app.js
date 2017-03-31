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
    that.logGetTemplate = function (templateName) {
        var props = { templateName: templateName };
        appInsights.trackEvent("GetLRSTemplate", props);

    if (typeof (mixpanel) !== 'undefined')
        mixpanel.track("GetLRSTemplate", { properties: this.addMixPanelProperties(props), measurements: null });
    }

    that.logAuthenticatedUser = function (authenticatedUserId) {
        if (authenticatedUserId) {
            if (typeof (mixpanel) !== 'undefined') {
                var userDetails = authenticatedUserId.split("#");
                if (userDetails.length === 2) {
                    mixpanel.alias(userDetails[1]);
                } else {
                    mixpanel.alias(authenticatedUserId);
                }
            }
            appInsights.setAuthenticatedUserContext(authenticatedUserId, '');
        }
    }

    that.logPageView = function ()
    {
        if (typeof (mixpanel) !== 'undefined') {
            var mixpaneluserid = getQueryVariable("correlationId");
            if (mixpaneluserid)
            {
                mixpanel.identify(mixpaneluserid);
            }
            mixpanel.track('LRS Deploy Page Viewed', { page: window.location, properties: that.addMixPanelProperties(null), measurements: null });
        }
    }

    that.addMixPanelProperties = function (properties) {
            properties = properties || {};
            properties['sitename'] = 'functions';
        }
    that.logDeploy = function (templateName) {
        var props = { templateName: templateName };
        appInsights.trackEvent("LRSDeployStarted", { templateName: templateName });
        if (typeof (mixpanel) !== 'undefined')
            mixpanel.track('LRSDeployStarted', { page: window.location, properties: that.addMixPanelProperties(props), measurements: null });
    }
        
    that.logDeploySucceeded = function (templateName) {
        var props = { templateName: templateName };
        appInsights.trackEvent("LRSDeploySucceeded", props);
        if (typeof (mixpanel) !== 'undefined')
            mixpanel.track('LRSDeploySucceeded', { page: window.location, properties: that.addMixPanelProperties(props), measurements: null });
    }

    that.logDeployFailed = function (templateName) {
        var props = { templateName: templateName };
        appInsights.trackEvent("LRSDeployFailed", props);
        if (typeof (mixpanel) !== 'undefined')
            mixpanel.track('LRSDeployFailed', { page: window.location, properties: that.addMixPanelProperties(props), measurements: null });
    }

    return that;
};

var constantsObj = function () {
    var that = {};
    var paramsObj = function () {
        var that = {};
        that.pollingInterval = 2000;
        return that;
    }

    that.params = paramsObj();

    return that;
}
function getQueryVariable(variable) {
    var query = window.location.search.substring(1);
    var vars = query.split("&");
    for (var i = 0; i < vars.length; i++) {
        var pair = vars[i].split("=");
        if (pair[0] == variable) { return pair[1]; }
    }
    return (false);

}

var telemetry = telemetryObj();
var constants = constantsObj();


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
                url: '/',
                templateUrl: 'ng/views/form.html',
                controller: 'FormController'
            })

        // send users to the form page 
        $urlRouterProvider.otherwise('/');
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
    .controller('FormController', ['$window', '$scope', '$location', '$http', function ($window, $scope, $location, $http) {
        // we will store all of our form data in this object
        $scope.formData = {};
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
        var portalWebSiteFormat = "https://portal.azure.com#resource/subscriptions/{0}/resourceGroups/{1}/providers/Microsoft.Web/sites/{2}/QuickStartSetting";
        var portalRGFormat = "https://portal.azure.com/#asset/HubsExtension/ResourceGroups/subscriptions/{0}/resourceGroups/{1}";
        var basePortalUrl= "https://portal.azure.com/";
        function initialize($scope, $http) {
            $scope.formData.templateName = getQueryVariable("templateName");
            telemetry.logPageView();

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

            // If we don't have the repository url, then don't init.  Also
            // if the user hit "back" from the next page, we don't re-init.

            if (!$scope.formData.templateName || $scope.formData.templateName.length === 0 || $scope.formData.subscriptions) {
                return;
            }

            $http({
                method: "get",
                url: "api/lrstemplate",
                params: {
                    "templateName": $scope.formData.templateName
                }
            })
            .then(function (result) {
                $scope.formData.userDisplayName = result.data.userDisplayName;
                $scope.formData.subscriptions = result.data.subscriptions;
                $scope.formData.tenants = result.data.tenants;
                $scope.formData.templateName = result.data.templateName;
                $scope.formData.appServiceName = result.data.appServiceName;
                $scope.formData.appServiceLocation = result.data.appServiceLocation;
                $scope.formData.templateNameUrl = result.data.templateUrl;
                $scope.formData.email= result.data.email;
                $scope.formData.newResourceGroup = {
                    name: result.data.resourceGroupName,
                    location: ""
                };
                telemetry.logAuthenticatedUser($scope.formData.email);
                // Select first subscription
                if ($scope.formData.subscriptions && $scope.formData.subscriptions.length > 0) {
                    var sub = $scope.formData.subscriptions[0];
                    $scope.formData.subscription = sub;
                    setDefaultRg(sub);
                }

                $scope.formData.params = [];
                    var param = paramObject();

                    param.name = "appserviceName";
                    param.type = "string";
                    param.value = result.data.appServiceName;
                    param.defaultValue = result.data.appServiceName;

                    $scope.formData.params.push(param);
              
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
                url: "api/lrsdeployments/" + subscriptionId,
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
            return true;
        }

        function getParamByName(params, name) {
            for (var i = 0; i < params.length; i++) {
                if (params[i].name.toLowerCase() === name.toLowerCase()) {
                    return params[i];
                }
            }
            return null;
        }

        function getDeployPayload(params) {
            var dataParams = {}
            var rg = $scope.formData.newResourceGroup ;
            $scope.formData.finalResourceGroup = rg;

            var nameParam =  { name: "appServiceName", value: $scope.formData.appServiceName };
            var locationParam =  { name: "appServiceLocation", value: $scope.formData.appServiceLocation};

            dataParams[nameParam.name] = { value: nameParam.value };
            dataParams[locationParam.name] = { value: locationParam.value };
            rg.location = locationParam.value;
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
                url: "api/lrsdeployments/" + subscriptionId + "/rg/" + resourceGroup.name,
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
                if (error || result.data.provisioningState === "Failed" || result.data.provisioningState === "Succeeded") {
                    $scope.formData.portalUrl = basePortalUrl;

                    $scope.formData.deploymentSucceeded = (result.data.provisioningState === "Succeeded");
                    if ($scope.formData.deploymentSucceeded)
                    {
                        $scope.formData.portalUrl = portalWebSiteFormat.format(
                        $scope.formData.subscription.subscriptionId,
                        $scope.formData.finalResourceGroup.name,
                        $scope.formData.appServiceName);
                        telemetry.logDeploySucceeded($scope.formData.templateName);
                    }
                    else
                    {
                        $scope.formData.portalUrl = portalRGFormat.format(
                        $scope.formData.subscription.subscriptionId,
                        $scope.formData.finalResourceGroup.name,
                        $scope.formData.appServiceName);
                        telemetry.logDeployFailed($scope.formData.templateName);
                    }
                    $window.location.href = $scope.formData.portalUrl;
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

    }]) // end FormController

})();
