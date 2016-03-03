function simpleState (options) {
    "use strict";

    if (!options) { options = {}; }
    
    this._state = null;
    
    /// name of the stored cookie
    this.cookieName = options.cookieName || "last-page-state";
    
    /// cookie persistance mode:
    /// page - valid for the current page only with long expiration date
    /// session - valid only for current session (short expiration date) and page
    /// site - valid for the role site with long expiration date
    /// site session - valid only for current session (short expiration date) but for the hole site
    this.persistence = options.persistence || "page";

    // check if we have HTML5 storage
    this.useHtml5Storage = function () {
        try {
            return 'localStorage' in window && window['localStorage'] !== null;
        } catch (e) {
            return false;
        }
    }();
        
    // select persistence mode: localStorage or cookie
    if (this.useHtml5Storage) {        
        if (this.persistence.indexOf("page") >= 0 || this.persistence === "session") {
            var p = window.location.href.split('://')[1].split('#')[0].split('?')[0].split('/');
            this.cookieName = (p.pop() || "") + "_" + this.cookieName;
            this.cookieName = (p.pop() || "") + "_" + this.cookieName;
        }
        if (this.persistence.indexOf("session") >= 0) {
            this.read = function (key) {
                return sessionStorage[key];
            };
            this.write = function (key, data) {
                sessionStorage[key] = data;
            };
        } else {
            this.read = function (key) {
                return localStorage[key];
            };
            this.write = function (key, data) {
                localStorage[key] = data;
            };
        }
    } else {
        // fallback to cookie usage
        // based on https://developer.mozilla.org/en-US/docs/Web/API/document.cookie
        this.read = function (sKey) {
            if (!sKey) { return null; }
            return decodeURIComponent(document.cookie.replace(new RegExp("(?:(?:^|.*;)\\s*" + encodeURIComponent(sKey).replace(/[\-\.\+\*]/g, "\\$&") + "\\s*\\=\\s*([^;]*).*$)|^.*$"), "$1")) || null;
        };
        this.write = function (sKey, sValue) {
            // prepare options
            var vEnd, sDomain, sPath, bSecure;
            if (this.persistence.indexOf("session") < 0) { vEnd = Infinity; }
            if (this.persistence.indexOf("site") >= 0) { sPath = "/"; }            

            if (!sKey || /^(?:expires|max\-age|path|domain|secure)$/i.test(sKey)) { return false; }
            var sExpires = "";
            if (vEnd) {
                switch (vEnd.constructor) {
                    case Number:
                        sExpires = vEnd === Infinity ? "; expires=Fri, 31 Dec 9999 23:59:59 GMT" : "; max-age=" + vEnd;
                        break;
                    case String:
                        sExpires = "; expires=" + vEnd;
                        break;
                    case Date:
                        sExpires = "; expires=" + vEnd.toUTCString();
                        break;
                }
            }
            document.cookie = encodeURIComponent(sKey) + "=" + encodeURIComponent(sValue) + sExpires + (sDomain ? "; domain=" + sDomain : "") + (sPath ? "; path=" + sPath : "") + (bSecure ? "; secure" : "");
            return true;
        };
    }
}

simpleState.prototype.load = function () {
    var c = this.read(this.cookieName);
    if (c) {
        this._state = JSON.parse(c);
    }
    if (!this._state) { this._state = {}; }
    return this;
};

simpleState.prototype.save = function () {
    if (!this._state) { return; }    
    // prepare cookie value and save
    this.write(this.cookieName, JSON.stringify(this._state));
};

simpleState.prototype.get = function (key, defaultValue) {
    // load data if necessary
    if (!this._state) { this.load(); }
    var st = this._state;    
    if (st) {
        var c = st[key];
        if (!c || (typeof c === "number" && isNaN(c))) { return defaultValue; }
        return c;
    }
    return defaultValue;
};

simpleState.prototype.set = function (key, value, skipAutoSave) {
    // load data if necessary
    if (!this._state) { this.load(); }
    // update value
    if (!value) { delete this._state[key]; }
    else { this._state[key] = value; }
    // try to save
    if (!skipAutoSave) {
        this.save();
    }
    return this;
};

simpleState.prototype.clear = function () {
    this._state = {};
    this.save();
};

var simplePageState = new simpleState({ persistence: "page", cookieName: "last-page-state" });
var simpleSessionState = new simpleState({ persistence: "session page", cookieName: "last-session-state" });