/*************************************
 * simpleTable
 * A basic Html Table generation helper
 * 
 * 
 * 1. How to create a table?
 *  <div id="usersTable"></div>
 *  $('#myTable').simpleTable();
 * 
 * 2. If I want a custom table layout...
 *  <table id="usersTable">
 *  <thead>...</thead>
 *  <tbody> Here goes the data whenever populate table is called!!! </tbody>
 *  </table>
 *  $('#myTable').simpleTable();
 * 
 * 3. How do I populate the table with data?
 *  $("#myTable").data('simpleTable').populateTable(myArrayOfData);
 * 
 * 4. How do I append data to an existing table?
 *  $("#myTable").data('simpleTable').populateTable(myArrayOfData);
 * 
 * 5. How to clear a table? 
 *  $("#myTable").data('simpleTable').clearTable();
 * 
 * advanced usage example: 
 * <input type="hidden" class="width-full" ng-select2-bind ng-model="filters.report" select2-query="dataSources.getReports()" allowClear multiple placeholder="Selecione um tipo de relatório" />
 * 
 * @param {function(data, rowId)} onRowSelection - function that will be called whenever a row is selected with the data item and row Id.
 * @param {string} selectable - table selection behavior: none, row.
 * @param {string} tableCss - css classes to add to the table. Examples: table-bordered table-condensed table-striped table-hover.
 * @param {function(id, data)} customDataParser - returns the list of cells for an item
 * @param {function(data):string[]} customHeaderCreation - return the list of headers
 * @param {column[]} columns - list of columns definitions: {field: '', header: '', cssClass: ''}
 * 
 * 
 *************************************/

(function ($) {
    "use strict";

    var SimpleTable = (function () {
        function SimpleTable(table) {
            this.table = table;
            this.idGen = 0;
            this.firstRowId = null;
            this.lastRowId = null;
            this.selectedRowId = null;
            this.rows = {};
            this.columns = null;
            this.onRowSelection = null;
            this.customDataParser = null;
            this.selectable = 'none';
            this.condensed = false;
        }

        SimpleTable.prototype.refresh = function () {
            var savedRows = this.rows;
            var savedCurrRowId = this.selectedRowId;
            this.clearTable();
            this.populateTable(savedRows);
            if (this.rows[savedCurrRowId]) {
                this.selectedRowId = savedCurrRowId;
            }
			return this;
        };

        SimpleTable.prototype.clearTable = function () {            
            var td = this.table.children('tbody');
            if (this.selectable === 'row') {
                td.find('tr').unbind('click');
            }
            td.empty();
            this.table.find('.simpleTable-internal').remove();
            this.rows = {};
            this.firstRowId = null;
            this.lastRowId = null;
            this.selectedRowId = null;
            this._tb = null;
            this.idGen = 0;
			return this;
        };

		SimpleTable.prototype.clearHeader = function () {
			this.columns = null;
			return this;
		};
		
		SimpleTable.prototype.addColumn = function (field, header) {
            if (!this.columns) {
				this.columns = [];
			}
			this.columns.push ({field: field, header: header});
			return this;
        };
		
        SimpleTable.prototype._createRow = function (id, data) {
            var row = "<tr st-row-id='" + (id || "") + "' style='cursor: pointer; cursor: hand;'>", i, len, parsed;
            if (this.customDataParser) {
                parsed = this.customDataParser(data);
                if (parsed && parsed.length) {
                    len = parsed.length;
                    if (len) {
                        for (i = 0; i < len; i++) {
                            row += "<td>" + (parsed[i] || "") + "</td>";
                        }
                    }
                }
            } else if (this.columns) {
                len = this.columns.length;
				for (i = 0; i < len; i++) {
					row += "<td>" + (data[this.columns[i].field] || "").toString() + "</td>";
				}
			} else {
                for (i in data) {
                    if (data.hasOwnProperty(i)) {
                        row += "<td>" + (data[i] || "").toString() + "</td>";
                    }
                }
            }

            row += "</tr>";
            return row;
        };

        SimpleTable.prototype._createHeader = function (firstItem) {
            var row = "<tr>", i, len, parsed, c;
            if (this.customHeaderCreation) {
                parsed = this.customHeaderCreation(firstItem);
                if (parsed && parsed.length) {
                    len = parsed.length;
                    if (len) {
                        for (i = 0; i < len; i++) {
                            row += "<th>" + (parsed[i] || "") + "</th>";
                        }
                    }
                }
            } else if (this.columns) {
                len = this.columns.length;
                for (i = 0; i < len; i++) {
                    c = this.columns[i];
                    if (c.cssClass) {
                        row += "<th class='" + c.cssClass + "'>";
                    } else {
                        row += "<th>";
                    }
                    row += (c.header || "") + "</th>";
                }
            } else {
                if (firstItem) {
                    this._lastHeader = [];
                    for (i in firstItem) {
                        if (firstItem.hasOwnProperty(i)) {
                            this._lastHeader.push(i);
                        }
                    }
                }
                if (this._lastHeader) {
                    len = this._lastHeader.length;
                    for (i = 0; i < len; i++) {
                        row += "<th>" + this._lastHeader[i]+ "</th>";
                    }
                }
            }

            row += "</tr>";

            this.table.find('thead').append(row);
        };

        SimpleTable.prototype._prepareTable = function (firstItem) {
            // try find table
            var t = this.table, th, s, c;
            if (!t.is('table')) {
                t = this.table.find('table');
            }
            // prepare table
            if (!t.length || !t.is('table')) {
                // create table if not found
                s = '<div class="table-responsive simpleTable-internal"><table class="table #tableCss#"><thead></thead><tbody></tbody></table></div>';
                c = 'table-bordered table-striped table-hover';
                if (this.tableCss) {
                    c = this.tableCss;
                }
                if (this.condensed) {
                    c = 'table-condensed ' + c;
                }
                this.table.append(s.replace('#tableCss#', c));
                t = this.table.find('table');
            } else {
                // create table elements
                if (!t.find('thead').length) {
                    t.append('<thead class="simpleTable-internal"></thead>');
                }
                if (!t.find('tbody').length) {
                    t.append('<tbody class="simpleTable-internal"></tbody>');
                }                
            }
            // check header
            th = t.find('thead > tr > th');
            if (!th.length) {
                this._createHeader(firstItem);
            }
        };

        SimpleTable.prototype._getTableBody = function (firstItem) {
            if (!this._tb) {
                this._tb = this.table.find('tbody');
                if (!this._tb.length) {
                    this._prepareTable(firstItem);
                    this._tb = this.table.find('tbody');
                }
            }
            return this._tb;            
        };

        SimpleTable.prototype.populateTable = function (list, append) {
            if (!append) {
                this.clearTable();
            }
            // sanity check
            if (!list || !list.length) {
                list = [];
            }
            // variables
            var tb = this._getTableBody(list[0]),
                i, id, len, rowsHtml = "", _this = this;

            len = list.length;
            // set first row id
            if (!this.firstRowId && len > 0) {
                this.firstRowId = this.idGen.toString();
            }
            for (i = 0; i < len; i++) {
                id = (this.idGen++).toString();
                this.rows[id] = list[i];
                rowsHtml += this._createRow(id, list[i]);
            }
            // set last row id
            if (id) {
                this.lastRowId = id;
            }

            // add rows to table
            tb.append(rowsHtml);
            // prepare event binding
            if (this.selectable === 'row') {
                tb.find('tr')
                    .unbind('click')
                    .bind('click', function (e) {
                        //e.preventDefault();
                        var rowId = $(this).attr('st-row-id');
                        _this.selectRow(rowId);
                    });
            }
			return this;
        };

        SimpleTable.prototype.selectRow = function (rowId) {
            // select first item if empty rowId
            if (!rowId || rowId === "first") {
                rowId = this.firstRowId;
            } else if (rowId === "last") {
                rowId = this.lastRowId;
            }
            // try to select row
            if (rowId) {
                var data = this.rows[rowId];
                if (data) {
                    if (this.selectedRowId) {
                        this.table.find('tr[st-row-id="' + this.selectedRowId + '"]').toggleClass('info', false);
                    }
                    this.selectedRowId = rowId;
                    this.table.find('tr[st-row-id="' + rowId + '"]').toggleClass('info', true);
                    if (this.onRowSelection) {
                        this.onRowSelection(data, rowId);
                    }
                }
            }
			return this;
        };

        SimpleTable.prototype.destroy = function () {
            this.clearTable();
            this.table.data('simpleTable', null);            
        };

        return SimpleTable;
    })();

    $.fn.simpleTable = function (options) {
        options = $.extend({
            onRowSelection: null,
            customDataParser: null,
            customHeaderCreation: null,
			columns: null,
			selectable: "row"//"multiple cell" // multiple single cell row checkbox none
        }, options);
        var tb = new SimpleTable($(this));
        this.data('simpleTable', tb);

        tb.onRowSelection = options.onRowSelection;
        tb.customDataParser = options.customDataParser;
        tb.customHeaderCreation = options.customHeaderCreation;
		tb.columns = options.columns;
		tb.selectable = options.selectable;
		tb.condensed = !!options.condensed;
		tb.tableCss = options.tableCss;

        return this;
    };

})(jQuery);