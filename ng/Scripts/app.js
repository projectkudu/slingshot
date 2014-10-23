var globalObjects = function(){
	var that = {};
	var repositoryUrl = null;

	return that;
}

var globals = globalObjects();

(function () {

var app = angular.module('azureDeploy', ['ngRoute', 'controllers']);

app.config(['$routeProvider', '$locationProvider', function($routeProvider, $locationProvider) {
	$routeProvider
    .when('/setup', {
        templateUrl: 'ng/views/Setup.html',
        controller: 'SetupCtrl',
        caseInsensitiveMatch: true
    })
    .when('/deploy', {
        templateUrl: 'ng/views/Deploy.html',
        controller: 'DeployCtrl',
        caseInsensitiveMatch: true
    })
    .otherwise({
        // redirectTo: '/'
        controller: 'SetupCtrl',
        templateUrl: 'ng/views/setup.html'
    });

	$locationProvider.html5Mode(true);

}]);

app.controller('MainController', ['$scope', '$location', function ($scope, $location) {
	$scope.clickNext = function(){
		window.location.href = 'https://localhost:44300/deploy';
	}

	function initialize(){
		globals.repositoryUrl = getQueryVariable("repository");
		// $location.url("/");
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
}]);

})();