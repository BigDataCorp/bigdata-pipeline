
var simpleDialog = function () {
    "use strict";
    var dlg = this;
    dlg._loadingCounter = 0;

    this.defaultOptions = {
        successButtonContent: "OK",
        cancelButtonContent: "Cancelar",
        hideSuccessBtn: false,
        hideCancelBtn: false,
        successButtonClass: "pull-right btn btn-default",
        cancelButtonClass: "pull-left btn btn-default"
    };

    this._create = function () {
        if (dlg.divMain) { return; }

        dlg.divMain = $("<div style='padding: 8px 8px 0px 8px; min-width: 250px; max-width: 100%;'></div>");

        dlg.divContent = $("<div style='min-height: 50px;'></div>");

        dlg.divLoading = $("<div style='text-align: center; padding: 16px;'><i class='fa fa-circle-o-notch fa-spin fa-4x'></i></div>");

        dlg.divMessage = $("<div></div>");

        dlg.divAction = $("<div class='clearfix'></div>");

        dlg.btnSuccess = $("<button type='button' style='min-width: 90px; max-width: 240px;'>OK</button>");
        dlg.btnCancel = $("<button type='button' style='min-width: 90px; max-width: 240px;'>Cancelar</button>");

        dlg.divAction.append(dlg.btnCancel);
        dlg.divAction.append(dlg.btnSuccess);

        dlg.divContent.append(dlg.divLoading);
        dlg.divContent.append(dlg.divMessage);

        dlg.divMain.append(dlg.divContent);
        dlg.divMain.append(dlg.divAction);

        var hideArea = $("<div style='display: none;'></div>");
        hideArea.append(dlg.divMain);
        $(document.body).append(hideArea);

        dlg.btnSuccess.click(function () { dlg.close(); if (dlg.onSuccess) { dlg.onSuccess(); } });
        dlg.btnCancel.click(function () { dlg.close(); if (dlg.onCancel) { dlg.onCancel(); } });

        dlg.isClosing = false;
        $(document).bind("cbox_closed", function () {
            dlg.isClosing = false;
            dlg.isOpening = false;
            dlg.isOpen = false;
            dlg._loadingCounter = 0;
        });
        $(document).bind("cbox_purge", function () {
            dlg.isClosing = false;
            dlg.isOpening = false;
            dlg.isOpen = false;
        });
        $(document).bind("cbox_complete", function () {
            dlg.isClosing = false;
            dlg.isOpening = false;
            dlg.isOpen = true;
            setTimeout(function () {
                dlg._onDisplay();
                if (dlg.lastOptions && typeof dlg.lastOptions.onDisplay === "function") {
                    dlg.lastOptions.onDisplay();
                }
            });
        });
    };

    this._onDisplay = function () {
        var e = dlg.divContent.find("input:first");
        if (!e.length) {
            e = dlg.lastOptions.hideCancelBtn ? dlg.btnSuccess : dlg.btnCancel;
        }
        e.focus();
        //this.resize();
    };
    this._executeTask = function () {
        dlg.timer = null;
        if (dlg._task === "open") {
            dlg._openDialog();
        } else if (dlg._task === "close") {
            dlg._closeDialog();
        } else if (dlg._task === "resize" && (!dlg.isClosing && !dlg.isOpening && dlg.isOpen)) {
            $.colorbox.resize();
        } else { // reset
            dlg.isClosing = false;
            dlg.isOpening = false;
            if ((dlg._task || "").split(".")[1] === "close") {
                dlg.isOpen = false;
            } else {
                dlg.isOpen = true;
            }
        }
        dlg._task = null;
    };

    this._setTask = function (task) {
        // ignore resize if another task is queued
        if (dlg._task && task === "resize") {
            return;
        }

        // stop timer
        if (dlg.timer) {
            window.clearTimeout(dlg.timer);
        }

        // restart timer
        if (task === "reset") {
            dlg.timer = setTimeout(dlg._executeTask, 150);
            task += "." + dlg._task;
        } else {
            dlg.timer = setTimeout(dlg._executeTask, 50);
        }
        // set task op
        dlg._task = task;
    };

    this._closeDialog = function () {
        if (!dlg.isOpening) {
            dlg.isClosing = true;
            $.colorbox.close();
            dlg._cleanUpLastDisplay();
            dlg._setTask("reset");
        } else {
            dlg._setTask("close");
        }
    };

    this._openDialog = function () {
        if (!dlg.isClosing && !dlg.isOpening) {
            dlg.divMain.show();
            dlg.isOpening = true;
            dlg.isOpen = false;
            // show dialog with colorbox
            $.colorbox({ inline: true, href: dlg.divMain, open: true, opacity: 0.5, closeButton: false, escKey: false, overlayClose: false, fixed: true, trapFocus: false, maxHeight: "100%", maxWidth: "100%", scrolling: true, speed: 200 });
            dlg._setTask("reset");
        } else {
            dlg.divMain.hide();
            dlg._setTask("open");
        }
    };

    this._displayDialog = function () {
        dlg._setTask("open");
    };

    this._cleanUpLastDisplay = function () {
        if (dlg.lastMsg instanceof jQuery) {
            if (dlg.lastMsgParent && dlg.lastMsgParent.length) {
                dlg.lastMsg.detach();
                dlg.lastMsgParent.append(dlg.lastMsg);
            } else {
                dlg.lastMsg.remove();
            }
        }
        dlg.lastMsg = null;
        dlg.lastMsgParent = null;
    };

    // construct the dialog options
    this._prepareOptions = function (msg, onSuccess, onCancel, hideCancelBtn) {
        var opt = (typeof msg === "object" && (msg.msg || msg.content) && !(msg instanceof jQuery)) ? msg : { msg: msg || "" };
        if (typeof onSuccess === "function") { opt.onSuccess = onSuccess; }
        if (typeof onCancel === "function") { opt.onCancel = onCancel; }
        if (hideCancelBtn === true) { opt.hideCancelBtn = hideCancelBtn; }
        // check content alias for msg
        if (!opt.msg) { opt.msg = opt.content; }
        // merge default options with custom options
        return $.extend({}, dlg.defaultOptions, opt);
    };

    this._prepareDialog = function (msg, onSuccess, onCancel, hideCancelBtn) {
        var opt = dlg._prepareOptions(msg, onSuccess, onCancel, hideCancelBtn);

        dlg._create();
        dlg.divLoading.hide();

        // clean up
        dlg._cleanUpLastDisplay();

        // display content
        msg = opt.msg;
        dlg.lastMsg = msg;
        dlg.lastOptions = opt;
        if (msg && msg instanceof jQuery) {
            dlg.lastMsgParent = msg.parent();
            if (dlg.lastMsgParent && dlg.lastMsgParent.length) {
                msg.detach();
            }
            dlg.divMessage.empty().append(msg).show();
        } else if (msg) {
            dlg.divMessage.html(msg).show();
        } else {
            dlg.divMessage.empty().hide();
        }

        // update form properties
        dlg.divMessage.toggleClass("alert", false);
        dlg.divMessage.toggleClass("alert-success", false);
        dlg.divMessage.toggleClass("alert-warning", false);
        dlg.divMessage.toggleClass("alert-danger", false);
        dlg.onSuccess = opt.onSuccess;
        dlg.onCancel = opt.onCancel;

        dlg.divAction.toggle(!opt.hideSuccessBtn || !opt.hideCancelBtn);
        dlg.btnSuccess.toggle(!opt.hideSuccessBtn);
        dlg.btnCancel.toggle(!opt.hideCancelBtn);
        if (opt.hideCancelBtn || !(opt.onSuccess || opt.onCancel)) {
            opt.hideCancelBtn = true;
            dlg.btnCancel.hide();
        } else {
            dlg.btnCancel.show();
        }
        // add button style
        if (!opt.hideSuccessBtn) { dlg.btnSuccess.removeClass().addClass(opt.successButtonClass); }
        if (!opt.hideCancelBtn) { dlg.btnCancel.removeClass().addClass(opt.cancelButtonClass); }

        // buttons content
        dlg.btnSuccess.html(opt.successButtonContent);
        dlg.btnCancel.html(opt.cancelButtonContent);
    };

    /**
     * closes an open simpleDialog.
     * @param {object} [closeOptions] - Select which simpleDialog to close.
     * @param {object} [closeOptions.ignore] - Do not close a specific dialog mode.
     * @param {object} [closeOptions.close] - Specific witch dialog mode to close.
     * @param {boolean} [closeOptions.loadingCounter] - Uses an internal counter of open loading dialogs to decide if we should close.
     * @example 
     * // how to close all dialog but error and warning dialog modes
     * simpleDialog.close({ ignore: { "error" : true, "warning": true }});
     * 
     * // how to close only the info and show dialog modes
     * simpleDialog.close({ close: { "info" : true, "show": true }});
     * 
     * // how to close only the loading dialog using the internal counter
     * simpleDialog.close({
     *      loadingCounter: true,
     *      close: { "loading": true }
     * });
     */
    this.close = function (closeOptions) {
        var mode = (typeof closeOptions === "object") ? closeOptions : mode = { loadingCounter: !!closeOptions };

        // check close options
        if (mode.ignore && mode.ignore[dlg._mode]) { return; }
        if (mode.close && !mode.close[dlg._mode]) { return; }

        // update internal counter
        dlg._loadingCounter = dlg._loadingCounter - 1;
        if (!mode.loadingCounter || dlg._loadingCounter <= 0) {
            dlg._loadingCounter = 0;
        }

        // close
        if (dlg._loadingCounter <= 0) {
            dlg._setTask("close");
            dlg._mode = "close";
        }
    };

    /**
     * open an error simpleDialog.
     * @param {string|object} [msg] - Dialog message, or option object.
     * @param {string|object} [msg.content] - Dialog message, or JQeury element to be attached to the dialog.
     * @param {function} [msg.onSuccess] - Function to be called on success button click.
     * @param {function} [msg.onCancel] - Function to be called on cancel button click.     
     * @param {string} [msg.successButtonContent] - Text of success action button.
     * @param {string} [msg.cancelButtonContent] - Text of cancel action button.     
     * @param {boolean} [msg.hideCancelBtn] - If the cancel action button should be hidden.
     * @param {boolean} [msg.hideSuccessBtn] - If the sucess action button should be hidden.
     * @param {function} [onSuccess] - Function to be called on success button click.
     * @param {function} [onCancel] - Function to be called on cancel button click.
     * @param {boolean} [hideCancelBtn] - If the cancel action button should be hidden.
     */
    this.error = function (msg, onSuccess, onCancel, hideCancelBtn) {
        dlg._mode = "error";
        dlg._prepareDialog(msg || "Falha na operação.<br/>Verifique sua conexão com a internet e tente novamente.<br/>Caso o problema persista, contecte o suporte técnico.", onSuccess, onCancel, hideCancelBtn);
        dlg.divMessage.toggleClass("alert", true);
        dlg.divMessage.toggleClass("alert-danger", true);
        dlg.divMessage.prepend("<i class='fa fa-warning fa-2x pull-left' style='margin-right: 18px;'></i>");
        dlg._loadingCounter = -10;
        dlg._displayDialog();
    };

    /**
     * open an success simpleDialog.
     * @param {string|object} [msg] - Dialog message, or option object.
     * @param {string|object} [msg.content] - Dialog message, or JQeury element to be attached to the dialog.
     * @param {function} [msg.onSuccess] - Function to be called on success button click.
     * @param {function} [msg.onCancel] - Function to be called on cancel button click.     
     * @param {string} [msg.successButtonContent] - Text of success action button.
     * @param {string} [msg.cancelButtonContent] - Text of cancel action button.     
     * @param {boolean} [msg.hideCancelBtn] - If the cancel action button should be hidden.
     * @param {boolean} [msg.hideSuccessBtn] - If the sucess action button should be hidden.
     * @param {function} [onSuccess] - Function to be called on success button click.
     * @param {function} [onCancel] - Function to be called on cancel button click.
     * @param {boolean} [hideCancelBtn] - If the cancel action button should be hidden.
     */
    this.success = function (msg, onSuccess, onCancel, hideCancelBtn) {
        dlg._mode = "success";
        dlg._prepareDialog(msg || "Operação realizada com sucesso.", onSuccess, onCancel, hideCancelBtn);
        dlg.divMessage.toggleClass("alert", true);
        dlg.divMessage.toggleClass("alert-success", true);
        dlg.divMessage.prepend("<i class='fa fa-check fa-2x pull-left' style='margin-right: 18px;'></i>");
        dlg._loadingCounter = -10;
        dlg._displayDialog();
    };

    /**
     * open an warning simpleDialog.
     * @param {string|object} [msg] - Dialog message, or option object.
     * @param {string|object} [msg.content] - Dialog message, or JQeury element to be attached to the dialog.
     * @param {function} [msg.onSuccess] - Function to be called on success button click.
     * @param {function} [msg.onCancel] - Function to be called on cancel button click.     
     * @param {string} [msg.successButtonContent] - Text of success action button.
     * @param {string} [msg.cancelButtonContent] - Text of cancel action button.     
     * @param {boolean} [msg.hideCancelBtn] - If the cancel action button should be hidden.
     * @param {boolean} [msg.hideSuccessBtn] - If the sucess action button should be hidden.
     * @param {function} [onSuccess] - Function to be called on success button click.
     * @param {function} [onCancel] - Function to be called on cancel button click.
     * @param {boolean} [hideCancelBtn] - If the cancel action button should be hidden.
     */
    this.warning = function (msg, onSuccess, onCancel, hideCancelBtn) {
        dlg._mode = "warning";
        dlg._prepareDialog(msg || "", onSuccess, onCancel, hideCancelBtn);
        dlg.divMessage.toggleClass("alert", true);
        dlg.divMessage.toggleClass("alert-warning", true);
        dlg.divMessage.prepend("<i class='fa fa-info-circle fa-2x pull-left' style='margin-right: 18px;'></i>");
        dlg._loadingCounter = -10;
        dlg._displayDialog();
    };

    /**
     * open an simpleDialog.
     * @param {string|object} [msg] - Dialog message, or option object.
     * @param {string|object} [msg.content] - Dialog message, or JQeury element to be attached to the dialog.
     * @param {function} [msg.onSuccess] - Function to be called on success button click.
     * @param {function} [msg.onCancel] - Function to be called on cancel button click.     
     * @param {string} [msg.successButtonContent] - Text of success action button.
     * @param {string} [msg.cancelButtonContent] - Text of cancel action button.     
     * @param {boolean} [msg.hideCancelBtn] - If the cancel action button should be hidden.
     * @param {boolean} [msg.hideSuccessBtn] - If the sucess action button should be hidden.
     * @param {function} [onSuccess] - Function to be called on success button click.
     * @param {function} [onCancel] - Function to be called on cancel button click.
     * @param {boolean} [hideCancelBtn] - If the cancel action button should be hidden.
     */
    this.info = function (msg, onSuccess, onCancel, hideCancelBtn) {
        dlg.show(msg, onSuccess, onCancel, hideCancelBtn);
        dlg._mode = "info";
    };

    /**
     * open an simpleDialog with custom content.
     * @param {string|object} [msg] - Dialog message, or option object.
     * @param {string|object} [msg.content] - Dialog message, or JQeury element to be attached to the dialog.
     * @param {function} [msg.onSuccess] - Function to be called on success button click.
     * @param {function} [msg.onCancel] - Function to be called on cancel button click.     
     * @param {string} [msg.successButtonContent] - Text of success action button.
     * @param {string} [msg.cancelButtonContent] - Text of cancel action button.     
     * @param {boolean} [msg.hideCancelBtn] - If the cancel action button should be hidden.
     * @param {boolean} [msg.hideSuccessBtn] - If the sucess action button should be hidden.
     * @param {function} [onSuccess] - Function to be called on success button click.
     * @param {function} [onCancel] - Function to be called on cancel button click.
     * @param {boolean} [hideCancelBtn] - If the cancel action button should be hidden.
     */
    this.show = function (msg, onSuccess, onCancel, hideCancelBtn) {
        dlg._mode = "show";
        dlg._prepareDialog(msg || "", onSuccess, onCancel, hideCancelBtn);
        dlg._loadingCounter = -10;
        dlg._displayDialog();
    };

    /**
     * open an loading simpleDialog.
     * @param {string|object} [msg] - Dialog message, or option object.
     * @param {string|object} [msg.content] - Dialog message, or JQeury element to be attached to the dialog.
     * @param {function} [msg.onSuccess] - Function to be called on success button click.
     * @param {function} [msg.onCancel] - Function to be called on cancel button click.     
     * @param {string} [msg.successButtonContent] - Text of success action button.
     * @param {string} [msg.cancelButtonContent] - Text of cancel action button.     
     * @param {boolean} [msg.hideCancelBtn] - If the cancel action button should be hidden.
     * @param {boolean} [msg.hideSuccessBtn] - If the sucess action button should be hidden.
     * @param {function} [onSuccess] - Function to be called on success button click.
     * @param {function} [onCancel] - Function to be called on cancel button click.
     * @param {boolean} [hideCancelBtn] - If the cancel action button should be hidden.
     */
    this.loading = function (msg) {
        dlg._mode = "loading";
        dlg._prepareDialog(msg);
        dlg.divLoading.show();
        dlg.divAction.hide();
        if (dlg._loadingCounter <= 0) { dlg._loadingCounter = 0; }
        ++dlg._loadingCounter;
        dlg._displayDialog();
    };

    /**
     * Request simpleDialog to recalculate its size based on the internal content.     
     */
    this.resize = function () {
        dlg._setTask("resize");
    };

};

simpleDialog = new simpleDialog();

