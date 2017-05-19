//https://jscompress.com/
// https://developer.mozilla.org/en-US/docs/DOM/window.setInterval
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
        window.appInsights.trackEvent("GetLRSTemplate", props);

    if (typeof (window.mixpanel) !== 'undefined')
        window.mixpanel.track("GetLRSTemplate", { properties: this.addMixPanelProperties(props), measurements: null });
    }

    that.logAuthenticatedUser = function (authenticatedUserId) {
        if (authenticatedUserId) {
            if (typeof (window.mixpanel) !== 'undefined') {
                var userDetails = authenticatedUserId.split("#");
                if (userDetails.length === 2) {
                    window.mixpanel.alias(userDetails[1]);
                } else {
                    window.mixpanel.alias(authenticatedUserId);
                }
            }
            window.appInsights.setAuthenticatedUserContext(authenticatedUserId, '');
        }
    }

    that.logPageView = function ()
    {
        if (typeof (window.mixpanel) !== 'undefined') {
            var mixpaneluserid = getQueryVariable("correlationId");
            if (mixpaneluserid)
            {
                window.mixpanel.identify(mixpaneluserid);
            }
            window.mixpanel.track('LRS Deploy Page Viewed', { page: window.location, properties: that.addMixPanelProperties(null), measurements: null });
        }
    }

    that.addMixPanelProperties = function (properties) {
            properties = properties || {};
            properties['sitename'] = 'functions';
        }
    that.logDeploy = function (templateName) {
        var props = { templateName: templateName };
        window.appInsights.trackEvent("LRSDeployStarted", { templateName: templateName });
        if (typeof (window.mixpanel) !== 'undefined')
            window.mixpanel.track('LRSDeployStarted', { page: window.location, properties: that.addMixPanelProperties(props), measurements: null });
    }
        
    that.logDeploySucceeded = function (templateName) {
        var props = { templateName: templateName };
        window.appInsights.trackEvent("LRSDeploySucceeded", props);
        if (typeof (window.mixpanel) !== 'undefined')
            window.mixpanel.track('LRSDeploySucceeded', { page: window.location, properties: that.addMixPanelProperties(props), measurements: null });
    }

    that.logDeployFailed = function (templateName) {
        var props = { templateName: templateName };
        window.appInsights.trackEvent("LRSDeployFailed", props);
        if (typeof (window.mixpanel) !== 'undefined')
            window.mixpanel.track('LRSDeployFailed', { page: window.location, properties: that.addMixPanelProperties(props), measurements: null });
    }

    return that;
};

var constantsObj = function () {
    var that = {};
    var paramsObj = function () {
        var that = {};
        that.pollingInterval = 2000;
        that.deployMessage1Interval = 30000;
        that.deployMessage2Interval = 60000;
        that.deployTimeoutInterval = 75000;
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
        if (pair[0] === variable) { return pair[1]; }
    }
    return (false);
}

var telemetry = telemetryObj();
var constants = constantsObj();

(function () {
    // app.js
    // create our angular app 
    // =============================================================================
    angular.module('formApp', [])
        // Custom filters
        // =============================================================================
        .filter('camelCaseToHuman', function() {
            return function(input) {
                var camelCase = input.name.charAt(0).toUpperCase() + input.name.substr(1).replace(/[A-Z]/g, ' $&');
                if (input.defaultValue === "") {
                    return camelCase + " (Optional)";
                }

                return camelCase;
            }
        })
        .controller('FormController', [
            '$window', '$scope', '$location', '$http', '$timeout', function ($window, $scope, $location, $http, $timeout) {
                $scope.deployTimerElapsed = false;
                $timeout(function () {
                    $scope.deployTimerElapsed = true;
                }, constants.params.deployTimeoutInterval);
                $timeout(function () {
                    insertMessageIfNotPresent($scope, (document.getElementById('deployingMessage1').innerHTML));
                }, constants.params.deployMessage1Interval);
                $timeout(function () {
                    insertMessageIfNotPresent($scope, (document.getElementById('deployingMessage2').innerHTML));
                }, constants.params.deployMessage2Interval);

                // we will store all of our form data in this object
                $scope.formData = {};
                var paramObject = function() {
                    var that = {};
                    that.name = null;
                    that.type = null;
                    that.defaultValue = null;
                    that.value = null;
                    return that;
                };

                var portalWebSiteFormat = "https://portal.azure.com/#resource/subscriptions/{0}/resourceGroups/{1}/providers/Microsoft.Web/sites/{2}/QuickStartSetting";
                var portalRGFormat = "https://portal.azure.com/#resource/subscriptions/{0}/resourceGroups/{1}/overview";
                var basePortalUrl = "https://portal.azure.com/";

                function insertMessageIfNotPresent($scope, mesg) {
                    if ($scope.formData.statusMesgs.indexOf(mesg) < 0) {
                        $scope.formData.statusMesgs.push(mesg);
                    }
                }

                function addStatusMesg($scope, result) {
                    if (result.data.operations && result.data.operations && result.data.operations.value) {
                        var ops = result.data.operations.value;
                        for (var i = ops.length - 1; i >= 0; i--) {
                            var mesg = ops[i].properties.targetResource.localizedMessage;
                            insertMessageIfNotPresent($scope, mesg);
                        }
                    }
                }

                function getStatus($scope, $http) {
                    var subscriptionId = $scope.formData.subscription.subscriptionId;
                    var resourceGroup = $scope.formData.finalResourceGroup;
                    var params;
                    $scope.formData.portalUrl = basePortalUrl;
                    if ($scope.formData.repoParamFound) {
                        params = {
                            "appServiceName": $scope.formData.appServiceName
                        };
                    }
                    $http({
                        method: "get",
                        url: "api/lrsdeployments/" + subscriptionId + "/rg/" + resourceGroup.name,
                        params: params
                    })
                        .then(function (result) {
                            addStatusMesg($scope, result);

                            // In some cases, errors will be hidden within the operations object.

                            var ops = result.data.operations;
                            var error = null;
                                if (ops && ops.value) {
                                    for (var i = 0; i < ops.value.length; i++) {
                                        var opProperties = ops.value[i].properties;
                                        if (opProperties.statusMessage &&
                                            opProperties.statusMessage.error) {
                                            error = opProperties.statusMessage.error.message;
                                        } else if (opProperties.provisioningState === "Failed" &&
                                            opProperties.statusMessage &&
                                            opProperties.statusMessage.message) {
                                            error = opProperties.statusMessage.message;
                                        }
                                    }
                                }
                                if (error || result.data.provisioningState === "Failed" || result.data.provisioningState === "Succeeded") {

                                $scope.formData.deploymentSucceeded = (result.data.provisioningState === "Succeeded");
                                if ($scope.formData.deploymentSucceeded) {
                                    $scope.formData.portalUrl = portalWebSiteFormat.format(
                                        $scope.formData.subscription.subscriptionId,
                                        $scope.formData.finalResourceGroup.name,
                                        $scope.formData.appServiceName);
                                    telemetry.logDeploySucceeded($scope.formData.templateName);
                                } else {
                                    $scope.formData.portalUrl = portalRGFormat.format(
                                        $scope.formData.subscription.subscriptionId,
                                        $scope.formData.finalResourceGroup.name,
                                        $scope.formData.appServiceName);
                                    telemetry.logDeployFailed($scope.formData.templateName);
                                }
                            $window.location.href = $scope.formData.portalUrl;
                            } else {
                                window.setTimeout(getStatus, constants.params.pollingInterval, $scope, $http);
                            }
                        },
                            function (result) {
                                $scope.formData.errorMesg = result.data.error;
                                telemetry.logDeployFailed('getdeploymentstatus');
                                $window.location.href = $scope.formData.portalUrl;
                            });
                }

                function getDeployPayload() {
                    var dataParams = {};
                    var rg = $scope.formData.newResourceGroup;
                    $scope.formData.finalResourceGroup = rg;

                    var nameParam = { name: "appServiceName", value: $scope.formData.appServiceName };
                    var locationParam = { name: "appServiceLocation", value: $scope.formData.appServiceLocation };

                    dataParams[nameParam.name] = { value: nameParam.value };
                    rg.location = locationParam.value;
                    return {
                        parameters: dataParams,
                        subscriptionId: $scope.formData.subscription.subscriptionId,
                        resourceGroup: rg,
                        templateUrl: $scope.formData.templateNameUrl,
                        repoUrl: sessionStorage.templateName
                    };
                }

                function deploy() {
                    var subscriptionId = $scope.formData.subscription.subscriptionId;
                    $scope.formData.portalUrl = basePortalUrl;

                    $scope.formData.deploymentSucceeded = false;
                    telemetry.logDeploy($scope.formData.templateName);
                    $scope.formData.deployPayload = getDeployPayload($scope.formData.params);

                    $http({
                            method: "post",
                            url: "api/lrsdeployments/" + subscriptionId,
                            data: $scope.formData.deployPayload
                        })
                        .then(function (result) {
                                window.setTimeout(getStatus, constants.params.pollingInterval, $scope, $http);
                            },
                            function (result) {
                                telemetry.logDeployFailed('postdeployment');
                                if (result.data != null && result.data.error != null) {
                                    $scope.formData.errorMesg = result.data.error;
                                    $window.location.href = $scope.formData.portalUrl;
                                }
                            });
                }

                function initialize($scope, $http) { 
                    $scope.formData.templateName = getQueryVariable("templateName");
                    $scope.formData.statusMesgs = [];
                    insertMessageIfNotPresent($scope, (document.getElementById('submittingMessage').innerHTML));

                    telemetry.logPageView();

                    if (!$scope.formData.templateName || $scope.formData.templateName.length === 0) {
                        if (sessionStorage.templateName) {
                            $scope.formData.templateName = sessionStorage.templateName;
                        } 
                    }

                    if ($scope.formData.templateName) {
                        sessionStorage.templateName = $scope.formData.templateName;
                        telemetry.logGetTemplate($scope.formData.templateName);
                    }
                    else {
                        telemetry.logDeployFailed('noTemplateName');
                        $window.location.href = "https://portal.azure.com";
                        return;
                    }
                    // If we don't have the repository url, then don't init.  Also
                    // if the user hit "back" from the next page, we don't re-init.
                    if (!$scope.formData.templateName || $scope.formData.templateName.length === 0 || $scope.formData.subscriptions) {
                        return;
                    }
                    $scope.formData.statusMesgs = [];

                    insertMessageIfNotPresent($scope, (document.getElementById('submittingMessage').innerHTML));

                    $http({
                            method: "get",
                            url: "api/lrstemplate",
                            params: {
                                "templateName": $scope.formData.templateName
                            }
                        })
                        .then(function (result) {
                                document.getElementById('loadingMessage').style.display = "none";
                                insertMessageIfNotPresent($scope, result.data.nextStatusMessage);
                                $scope.formData.subscription = result.data.subscription;
                                $scope.formData.tenants = result.data.tenants;
                                $scope.formData.templateName = result.data.templateName;
                                $scope.formData.appServiceName = result.data.appServiceName;
                                $scope.formData.appServiceLocation = result.data.appServiceLocation;
                                $scope.formData.templateNameUrl = result.data.templateUrl;
                                $scope.formData.email = result.data.email;
                                $scope.formData.newResourceGroup = {
                                    name: result.data.resourceGroupName,
                                    location: ""
                                };
                                telemetry.logAuthenticatedUser($scope.formData.email);

                                $scope.formData.params = [];
                                var param = paramObject();

                                param.name = "appserviceName";
                                param.type = "string";
                                param.value = result.data.appServiceName;
                                param.defaultValue = result.data.appServiceName;

                                $scope.formData.params.push(param);

                                deploy();
                            },
                            function(result) {
                                if (result.data) {
                                    $scope.formData.errorMesg = result.data.error;
                                }
                            });
                }

                initialize($scope, $http);
            }
        ]); // end FormController

})();
