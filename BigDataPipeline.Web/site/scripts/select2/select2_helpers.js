﻿/*************************************
 * select2 common and userful helper methods
 **************************************/

function select2_createSearchChoice(term, data) {
    var r = _select2_internalSearch(data, term, 'equals', 1);
    if (!r.length) { return { id: term, text: term }; }
}

function select2_prepareData(data) {
    if (!data || !data.length) { return []; }
    if (typeof data[0] === 'object') { return data; }
    var results = [], i, l = data.length, s;
    for (i = 0; i < l; ++i) {
        s = data[i];
        results[i] = { id: s, text: s };
    }
    return results;
}

function select2_initSelectionSimple(element, callback, data) {
    var k = element.val(), r, n = '';
    if (k) {
        if (data && data.length) {
            r = _select2_internalSearch(data, k, 'equals', 1);
            if (r.length) { n = r[0]; }
        } else {
            n = { id: k, text: k };
        }
    }
    callback(n);
}

function select2_initSelectionMulti(element, callback, data) {
    var k = element.val(), r, n = [], i;
    if (k) {
        k = k.split(",");
        for (i = 0; i < k.length; ++i) {
            if (data && data.length) {
                r = _select2_internalSearch(data, k[i], 'equals', 1);
                if (r.length) { n[n.length] = r[0].children ? r[0].children[0] : r[0]; }
            } else {
                n[n.length] = { id: k[i], text: k[i] };
            }
        }
    }
    callback(n);
}

function _select2_internalSearch(data, term, mode, limit) {
    var i, l, n, c, r = [], equals;
    // sanity checks
    limit = limit || 2147483647;
    if (!data || !data.length) { return []; }
    // prepare search term
    if (!term) { return data.length <= limit ? data : data.slice(0, limit); }
    term = select2_prepareSearchTerm(term);

    // prepare comparison method
    if (mode === 'equals') {
        equals = function (x, y) { return select2_prepareSearchTerm(x) === y; };
    } else {
        equals = function (x, y) { return select2_prepareSearchTerm(x).indexOf(y) !== -1; };
    }

    // run comparison
    for (i = 0, l = data.length; i < l; ++i) {
        n = data[i];
        if (typeof n === 'string') {
            if (equals(n, term)) { r[r.length] = { id: n, text: n }; }
        } else if (n.children) {
            c = { id: n.id, text: n.text, disabled: n.disabled };
            c.children = _select2_internalSearch(n.children, term, limit);
            r[r.length] = c;
        } else if (equals(n.text, term) || equals(n.id, term)) {
            r[r.length] = n;
        }
        if (r.length >= limit) { break; }
    }
    return r;
}

function select2_prepareQuery(options, rawData) {
    if (rawData && typeof rawData.promise === 'function') {
        $.when(rawData).then(function (r) { select2_internalPrepareQuery(options, (r && r.data) ? r.data : (r && r.length) ? r : []); });
    } else {
        select2_internalPrepareQuery(options, rawData);
    }
}

function select2_internalPrepareQuery(options, rawData) {
    var filteredData = options.context, pageSize = options.pageSize || 40,
        startIndex = (options.page - 1) * pageSize;

    if (!filteredData) {
        rawData = select2_prepareData(rawData);
        if (options.term) {
            filteredData = _select2_internalSearch(rawData, options.term, 'contains');
        } else {
            filteredData = rawData;
        }
    }

    options.callback({
        context: filteredData,
        results: filteredData.slice(startIndex, startIndex + pageSize),
        more: (startIndex + pageSize) < filteredData.length
    });
}

/*************************************
 * remove accentuation
 *************************************/
var select2_DiacriticsRemovalList = [
    { 'base': 'a', 'letters': '\u0061\u24D0\uFF41\u1E9A\u00E0\u00E1\u00E2\u1EA7\u1EA5\u1EAB\u1EA9\u00E3\u0101\u0103\u1EB1\u1EAF\u1EB5\u1EB3\u0227\u01E1\u00E4\u01DF\u1EA3\u00E5\u01FB\u01CE\u0201\u0203\u1EA1\u1EAD\u1EB7\u1E01\u0105\u2C65\u0250' },
    { 'base': 'c', 'letters': '\u0063\u24D2\uFF43\u0107\u0109\u010B\u010D\u00E7\u1E09\u0188\u023C\uA73F\u2184' },
    { 'base': 'e', 'letters': '\u0065\u24D4\uFF45\u00E8\u00E9\u00EA\u1EC1\u1EBF\u1EC5\u1EC3\u1EBD\u0113\u1E15\u1E17\u0115\u0117\u00EB\u1EBB\u011B\u0205\u0207\u1EB9\u1EC7\u0229\u1E1D\u0119\u1E19\u1E1B\u0247\u025B\u01DD' },
    { 'base': 'i', 'letters': '\u0069\u24D8\uFF49\u00EC\u00ED\u00EE\u0129\u012B\u012D\u00EF\u1E2F\u1EC9\u01D0\u0209\u020B\u1ECB\u012F\u1E2D\u0268\u0131' },
    { 'base': 'n', 'letters': '\u006E\u24DD\uFF4E\u01F9\u0144\u00F1\u1E45\u0148\u1E47\u0146\u1E4B\u1E49\u019E\u0272\u0149\uA791\uA7A5' },
    { 'base': 'o', 'letters': '\u006F\u24DE\uFF4F\u00F2\u00F3\u00F4\u1ED3\u1ED1\u1ED7\u1ED5\u00F5\u1E4D\u022D\u1E4F\u014D\u1E51\u1E53\u014F\u022F\u0231\u00F6\u022B\u1ECF\u0151\u01D2\u020D\u020F\u01A1\u1EDD\u1EDB\u1EE1\u1EDF\u1EE3\u1ECD\u1ED9\u01EB\u01ED\u00F8\u01FF\u0254\uA74B\uA74D\u0275' },
    { 'base': 'u', 'letters': '\u0075\u24E4\uFF55\u00F9\u00FA\u00FB\u0169\u1E79\u016B\u1E7B\u016D\u00FC\u01DC\u01D8\u01D6\u01DA\u1EE7\u016F\u0171\u01D4\u0215\u0217\u01B0\u1EEB\u1EE9\u1EEF\u1EED\u1EF1\u1EE5\u1E73\u0173\u1E77\u1E75\u0289' }
];

var select2_replaceMap = null;

// "what?" version ... http://jsperf.com/diacritics/12
function select2_prepareSearchTerm(str) {
    // initialization
    if (!select2_replaceMap) {
        var i, j, letters;
        select2_replaceMap = {};
        for (i = 0; i < select2_DiacriticsRemovalList.length; i++) {
            letters = select2_DiacriticsRemovalList[i].letters.split("");
            for (j = 0; j < letters.length ; j++) {
                select2_replaceMap[letters[j]] = select2_DiacriticsRemovalList[i].base;
            }
        }
    }

    // replace
    return (str || "").toLowerCase().replace(/[^\u0000-\u007E]/g, function (a) {
        return select2_replaceMap[a] || a;
    });
}

/*************************************
 * Angular JS 
 * A basic select2 Directive definition
 * to be used by an angularjs application
 * 
 * Some references about creating an angularjs directive
 * http://weblogs.asp.net/dwahlin/creating-custom-angularjs-directives-part-i-the-fundamentals
 * http://weblogs.asp.net/dwahlin/creating-custom-angularjs-directives-part-2-isolate-scope
 * http://weblogs.asp.net/dwahlin/creating-custom-angularjs-directives-part-3-isolate-scope-and-function-parameters
 * http://henriquat.re/directives/advanced-directives-combining-angular-with-existing-components-and-jquery/angularAndJquery.html
 * http://www.sitepoint.com/practical-guide-angularjs-directives/
 * https://amitgharat.wordpress.com/2013/06/08/the-hitchhikers-guide-to-the-directive/
 * 
 *  How to add this directive to an application?
 * ng_app.directive('ngSelect2Bind', ['$q', '$timeout', ngSelect2BindDirective]);
 * 
 * basic usage example: 
 * <input type="hidden" ng-select2-bind ng-model="mySelectedValue" select2-query="getPossibleValues()" />
 * 
 * advanced usage example: 
 * <input type="hidden" class="width-full" ng-select2-bind ng-model="filters.report" select2-query="dataSources.getReports()" allowClear multiple placeholder="Selecione um tipo de relatório" />
 * 
 * @param {(string|string[])} ng-model - bind the scope property with select2 values. It will use the id field of select2 data structure, example: { id: '', text: '' }.
 * @param {function} select2-query - function that will return select2 possible values. It can return an array or a jQuery/Angular deferred/promise with the result array [or an object like { data: [] }].
 * @param {bool=} multiple - if multiple values can be selected or only a single one
 * @param {bool=} disable-validation - if a programatic value initialization should be checked agains the possible values returned by selec2-query.
 * @param {string=} placeholder - the text to be displayed inside an empty select2.
 * @param {bool=} allow-clear - if the user can clear the select2 selection. 
 * 
 * 
 *************************************/
function ngSelect2BindDirective($q, $timeout) {
    function valuesAreEqual(v1, v2) {
        // note: select2 values could be string or array
        if (!v1 || !v2) { return v1 === v2; }
        if (v1.length !== v2.length) { return false; }
        // if string, compare as string
        if (typeof v1 === 'string') { return v1 === v2; }
        // compare as array
        for (var i = 0, len = v1.length; i < len; i++) {
            if (v1[i] !== v2[i]) { return false; }
        }
        return true;
    }

    function checkEnabledAttribute(attr, defaultValue) {
        if (defaultValue && (!attr && attr !== '')) { return defaultValue; }
        return !((!attr && attr !== '') || attr === 'false');
    }

    return {
        restrict: 'A', //E = element, A = attribute, C = class, M = comment    
        // responsible for registering DOM listeners as well as updating the DOM
        link: function (scope, element, attrs, controller) {
            //$(element).attrs("type", "hidden");
            // prepare possible attributes
            attrs.multiple = checkEnabledAttribute(attrs.multiple);
            attrs.allowClear = checkEnabledAttribute(attrs.allowClear);
            attrs.disableValidation = checkEnabledAttribute(attrs.disableValidation);
            scope.hasChangeListener = !!attrs.ngChange && typeof scope.ngChange === 'function';

            // bind onChange event and create select2 component
            // set change event
            element.on("change", function (e) {
                // use a timeout method to safelly signal angularjs to refresh its context
                $timeout(function () {
                    // update only if values differs
                    if (!valuesAreEqual(scope.ngModel, e.val)) {
                        scope.ngModel = e.val;
                    }
                    // signal change
                    if (scope.hasChangeListener) {
                        $timeout(scope.ngChange);
                    }
                });
            }).select2({
                placeholder: attrs.placeholder,
                allowClear: attrs.allowClear,
                initSelection: function (element, callback) {
                    if (!element || !element.val()) {
                        return;
                    }
                    var method = attrs.multiple ? select2_initSelectionMulti : select2_initSelectionSimple;
                    // simply put the new value or get possible value before initializing values
                    if (attrs.disableValidation) {
                        method(element, callback);
                    } else {
                        $q.when(scope.select2Query()).then(function (r) {
                            method(element, callback, (r && r.data) ? r.data : (r && r.length) ? r : []);
                        });
                    }
                },
                multiple: attrs.multiple,
                query: function (options) {
                    // select2Query can return the data (array or promise) or
                    // return nothing and implement the select2 query interface
                    var promise = scope.select2Query({ options: options });
                    // use jquery promises or array of data
                    if (promise) {
                        $q.when(promise).then(function (r) {
                            select2_prepareQuery(options, (r && r.data) ? r.data : (r && r.length) ? r : []);
                        });
                    }
                },
                formatNoMatches: attrs.formatNoMatches || undefined
            });

            // set intial value
            if (scope.ngModel) {
                element.select2('val', scope.ngModel, true);
            }

            // set binding between field and select2 component
            function updateSelectedValue(newValue, oldValue) {
                if (newValue !== oldValue && !valuesAreEqual(newValue, element.select2('val'))) {
                    // update select2 values and also trigger the change event to make sure we got the correct selected values
                    element.select2('val', newValue, true);
                }
            }
            scope.$watch('ngModel', updateSelectedValue);
        },
        // if a new scope should be created 
        // and how to bind our scope variables
        scope: {
            ngModel: '=',
            select2Query: '&',
            ngChange: '&'
        } //@ reads the attribute value, = provides two-way binding, & works with functions        
    };
}
