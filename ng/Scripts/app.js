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


// var globalObjects = function(){
// 	var that = {};
// 	// var repositoryUrl = null;

// 	return that;
// }

// var globals = globalObjects();

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
			templateUrl: '/ng/views/form.html',
			controller: 'FormController'
		})
		
		// nested states 
		// each of these sections will have their own view
		// url will be nested (/form/setup)
		.state('form.setup', {
			url: '/setup',
			templateUrl: '/ng/views/form-setup.html',
			controller: 'FormSetupController'

		})
		
		// url will be /form/deploy
		.state('form.deploy', {
			url: '/deploy',
			templateUrl: '/ng/views/form-deploy.html',
			controller: 'FormDeployController'
		})
		
		// url will be /form/payment
		.state('form.payment', {
			url: '/payment',
			templateUrl: '/ng/views/form-payment.html'
		});
		
	// catch all route
	// send users to the form page 
	$urlRouterProvider.otherwise('/form/setup');
})

// our controller for the form
// =============================================================================
.controller('FormController', ['$scope', '$location', function($scope, $location) {
	
	// we will store all of our form data in this object
	$scope.formData = {};
	
	// function to process the form
	$scope.processForm = function() {
		alert('awesome!');
	};

	////////////////////
	// Private Methods
	////////////////////

	function initialize(){
		$scope.formData.repositoryUrl = getQueryVariable("repository");
		if(!$scope.formData.repositoryUrl || $scope.formData.repositoryUrl.length ==0){
			$scope.formData.repositoryUrl = document.referrer;

			if(!$scope.formData.repositoryUrl || $scope.formData.repositoryUrl.length ==0){
				alert("No repository detected.");
			}
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
	}

	initialize();
}])

.controller('FormSetupController', ['$scope', '$http', function($scope, $http){
	var paramObject = function(){
		var that = {};
		that.name = null;
		that.type = null;
		that.allowedValues = [];
		that.defaultValue = null;
		that.value = null;
		return that;
	};

	function initialize($scope, $http){
		$http({
		    method: "get",
		    url: "api/template",
		    params: {
		    	"repositoryUrl" : $scope.formData.repositoryUrl
		    }
		})
		.then(function(result){
			$scope.formData.template = result.data.template;
			$scope.formData.subscriptions = result.data.subscriptions;
			$scope.formData.siteLocations = result.data.siteLocations;
			$scope.formData.templateUrl = result.data.templateUrl;
			$scope.formData.repositoryUrl = result.data.repositoryUrl;

			$scope.formData.params = [];
			var parameters = $scope.formData.template.parameters;
			for(var name in parameters){
				var parameter = parameters[name];
				var param = paramObject();
				
				param.name = name;
				param.type = parameter.type;
				param.allowedValues = parameter.allow;
				param.defaultValue = parameter.defaultValue;

				$scope.formData.params.push(param);
			}
		},
		function(error){
			alert(error.statusText)
		});
	}

	$scope.showParam = function(param){
		if(param.defaultValue){
			param.value = param.defaultValue;
			return false;
		}
		else{
			var name = param.name.toLowerCase();
			if(name === 'repourl' && $scope.formData.repositoryUrl){
				param.value = $scope.formData.repositoryUrl;
				return false;
			}
			else if(name === 'hostingplanname'){
				return false;
			}
		}

		return true;
	}

	$scope.canMoveToNextStep = function(){
		if($scope.formData.subscription && $scope.formData.params && $scope.formData.siteNameAvailable){
			var params = $scope.formData.params;
			for(var i = 0; i < params.length; i++){

				if(params[i].name === 'hostingPlanName' && $scope.formData.siteName){
					params[i].value = $scope.formData.siteName;
				}				

				if(!params[i].value){
					return false;
				}
			}

			return true;
		}

		return false;
	}

	$scope.checkSiteName = function(siteName){
		$scope.formData.querySiteName = siteName;
		window.setTimeout(querySiteName, 250, $scope, $http, siteName);
	}

	function querySiteName($scope, $http, siteName){
		if(siteName && $scope.formData.querySiteName === siteName){
			var subscriptionId = $scope.formData.subscriptions[1].subscriptionId
			$http({
			    method: "get",
			    url: "api/subscriptions/"+subscriptionId+"/sites/"+siteName
			})
			.then(function(result){
				if(result.data.siteName === $scope.formData.querySiteName){
					$scope.formData.siteNameAvailable = result.data.isAvailable;
					$scope.formData.siteName = result.data.siteName;
				}
			},function(result){
				alert(result.statusText);
			});
		}
	}

	initialize($scope, $http);

}]) // end FormSetupController

.controller('FormDeployController', ['$scope', '$http', function($scope, $http){
	$scope.formData.statusMesgs = [];
	$scope.formData.statusId = null;

	$scope.showError = function(){
		$('#errorModal').modal('show');
	}

	function initialize($scope, $http){
		var dataParams = {}
		var subscriptionId = $scope.formData.subscription.subscriptionId;

		var params = $scope.formData.params;
		for(var i = 0; i < params.length; i++){
			dataParams[params[i].name] = {value : params[i].value};
		}

		$scope.formData.statusMesgs.push("Submitting Deployment...");
		$http({
		    method: "post",
		    url: "api/deployments/"+subscriptionId,
		    params:{
		    	"templateUrl" : $scope.formData.templateUrl,
		    },
		    data: dataParams
		})
		.then(function(result){
			$scope.formData.statusMesgs.push("Deployment Started...");

			$scope.formData.statusId = window.setTimeout(getStatus, 5000, $scope, $http, result.data.deploymentUrl);
		},
		function(result){
			$scope.formData.errorMesg = result.data.error;
		});
	}

	function getStatus($scope, $http, deploymentUrl){
		var subscriptionId = $scope.formData.subscription.subscriptionId;
		var siteName = $scope.formData.siteName;

		$http({
		    method: "get",
		    url: "api/deployments/"+subscriptionId+"/sites/"+siteName
 		})
		.then(function(result){
			if(result.data.provisioningState === "Failed"){
				// etodo: add better error
				$scope.formData.errorMesg = "Failed";
			}
			else if(result.data.provisioningState === "Succeeded"){
				// $scope.formData.statusMesgs.push("Deployment Complete.");
				$scope.formData.siteUrl = result.data.siteUrl;
			}
			else{
				if(!$scope.formData.waitingForDeployment){
					$scope.formData.statusMesgs.push("Working...");	
					$scope.formData.waitingForDeployment = true;
				}
				$scope.formData.statusId = window.setTimeout(getStatus, 1000, $scope, $http, deploymentUrl);

			}
		},
		function(result){
			$scope.formdata.errorMesg = result.data.error;
		});
	}

	initialize($scope, $http);

}]);  // end FormDeployController

})();


// (function () {

// var app = angular.module('azureDeploy', ['ngRoute', 'controllers']);

// app.config(['$routeProvider', '$locationProvider', function($routeProvider, $locationProvider) {
// 	$routeProvider
//     .when('/setup', {
//         templateUrl: 'ng/views/Setup.html',
//         controller: 'SetupCtrl',
//         caseInsensitiveMatch: true
//     })
//     .when('/deploy', {
//         templateUrl: 'ng/views/Deploy.html',
//         controller: 'DeployCtrl',
//         caseInsensitiveMatch: true
//     })
//     .otherwise({
//         // redirectTo: '/'
//         controller: 'SetupCtrl',
//         templateUrl: 'ng/views/setup.html'
//     });

// 	$locationProvider.html5Mode(true);

// }]);

// app.controller('MainController', ['$scope', '$location', function ($scope, $location) {
// 	$scope.clickNext = function(){
// 		window.location.href = 'https://localhost:44300/deploy';
// 	}

// 	function initialize(){
// 		globals.repositoryUrl = getQueryVariable("repository");
// 		// $location.url("/");
// 	}

// 	function getQueryVariable(variable) {
// 	    var query = window.location.search.substring(1);
// 	    var vars = query.split('&');
// 	    for (var i = 0; i < vars.length; i++) {
// 	        var pair = vars[i].split('=');
// 	        if (decodeURIComponent(pair[0]) == variable) {
// 	            return decodeURIComponent(pair[1]);
// 	        }
// 	    }
// 	}


// 	initialize();
// }]);

// })();