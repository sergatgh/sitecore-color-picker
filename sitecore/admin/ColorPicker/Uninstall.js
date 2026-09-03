(function () {
    "use strict";

    var STEP_ORDER = ["welcome", "1", "2", "3", "4"];
    var state = { report: null };

    // ---- low-level helpers -------------------------------------------------

    function escapeHtml(value) {
        return String(value == null ? "" : value).replace(/[&<>"']/g, function (c) {
            return { "&": "&amp;", "<": "&lt;", ">": "&gt;", "\"": "&quot;", "'": "&#39;" }[c];
        });
    }

    function apiCall(action, opts) {
        opts = opts || {};
        var method = opts.method || "GET";
        var headers = {};
        var body;

        if (method === "POST") {
            headers["Content-Type"] = "application/json";
            headers["X-ColorPicker-Token"] = window.ColorPickerToken;
            body = JSON.stringify(opts.body || {});
        }

        return fetch("UninstallApi.ashx?action=" + encodeURIComponent(action), {
            method: method,
            headers: headers,
            body: body,
            credentials: "same-origin"
        }).then(function (response) {
            return response.json().catch(function () { return null; }).then(function (data) {
                if (!response.ok || (data && data.success === false)) {
                    var message = (data && data.message) || ("Request failed (" + response.status + ").");
                    var error = new Error(message);
                    error.details = data ? JSON.stringify(data, null, 2) : null;
                    throw error;
                }
                return data;
            });
        });
    }

    // A scan/manifest ("GET") response has no top-level "success" flag to check - it's just
    // data - so failures there are surfaced as a plain HTTP-status error by apiCall above.
    function withSpinner(button, fn) {
        var spinner = document.createElement("span");
        spinner.className = "cp-spinner";
        button.disabled = true;
        button.appendChild(spinner);

        return Promise.resolve().then(fn).finally(function () {
            button.disabled = false;
            if (spinner.parentNode) {
                spinner.parentNode.removeChild(spinner);
            }
        });
    }

    function showBanner(message, details) {
        var banner = document.getElementById("cp-banner");
        banner.hidden = false;
        banner.querySelector(".cp-banner-headline").textContent = message;

        var toggle = banner.querySelector(".cp-banner-toggle");
        var detailsEl = banner.querySelector(".cp-banner-details");

        if (details) {
            toggle.hidden = false;
            detailsEl.hidden = true;
            detailsEl.textContent = details;
            toggle.onclick = function () { detailsEl.hidden = !detailsEl.hidden; };
        } else {
            toggle.hidden = true;
            detailsEl.hidden = true;
        }
    }

    function hideBanner() {
        document.getElementById("cp-banner").hidden = true;
    }

    function goTo(step) {
        var index = STEP_ORDER.indexOf(step);
        Array.prototype.forEach.call(document.querySelectorAll("#cp-steps li"), function (li) {
            var i = STEP_ORDER.indexOf(li.getAttribute("data-step"));
            li.classList.toggle("active", i === index);
            li.classList.toggle("done", i >= 0 && i < index);
        });
        Array.prototype.forEach.call(document.querySelectorAll(".cp-panel"), function (p) {
            p.classList.remove("active");
        });
        document.getElementById("cp-panel-" + step).classList.add("active");
        hideBanner();
        ensureStepData(step);
    }

    // Steps 1-3 share a single "scan" call (state.report carries all three lists), so once any
    // of them has been scanned there is no need to hit the network again just because a
    // different step's table hasn't been rendered yet - render it from the cached report instead.
    function ensureStepData(step) {
        if (step !== "1" && step !== "2" && step !== "3") {
            return;
        }

        if (state.report) {
            if (step === "1") { renderStep1(); }
            else if (step === "2") { renderStep2(); }
            else { renderStep3(); }
            return;
        }

        var button = document.querySelector('[data-action="scan-' + step + '"]');
        if (step === "1") { scanStep1(button); }
        else if (step === "2") { scanStep2(button); }
        else { scanStep3(button); }
    }

    // ---- table rendering -----------------------------------------------------

    function statusPill(state) {
        if (state === "success") {
            return '<span class="cp-pill cp-pill-success">Success</span>';
        }
        if (state === "problem") {
            return '<span class="cp-pill cp-pill-problem">Problem</span>';
        }
        return "";
    }

    function renderTable(tableId, emptyId, allButtonEl, rows, headers, rowHtmlFn) {
        var table = document.getElementById(tableId);
        var empty = document.getElementById(emptyId);

        if (!rows.length) {
            table.style.display = "none";
            empty.hidden = false;
            if (allButtonEl) {
                allButtonEl.hidden = true;
            }
            return;
        }

        empty.hidden = true;
        table.style.display = "";
        if (allButtonEl) {
            allButtonEl.hidden = false;
        }

        table.innerHTML =
            "<thead><tr>" + headers.map(function (h) { return "<th>" + h + "</th>"; }).join("") + "<th></th></tr></thead>" +
            "<tbody>" + rows.map(rowHtmlFn).join("") + "</tbody>";
    }

    function linkCell(usage) {
        return '<td class="cp-path"><a href="' + escapeHtml(usage.ContentEditorUrl) + '" target="_blank" rel="noopener">' +
            escapeHtml(usage.ItemPath) + "</a></td>";
    }

    // ---- step 1: Color Picker-typed fields -----------------------------------

    function scanStep1(button) {
        return withSpinner(button, function () {
            return apiCall("scan").then(function (report) {
                state.report = report;
                renderStep1();
            }).catch(function (err) { showBanner(err.message, err.details); });
        });
    }

    function renderStep1() {
        var rows = (state.report && state.report.ColorPickerTypedFields) || [];
        var allButton = document.querySelector('[data-action="convert-all"]');
        renderTable("cp-table-1", "cp-empty-1", allButton, rows,
            ["Database", "Template", "Path"],
            function (f) {
                return "<tr data-db=\"" + escapeHtml(f.DatabaseName) + "\" data-id=\"" + escapeHtml(f.ItemId) + "\">" +
                    "<td>" + escapeHtml(f.DatabaseName) + "</td>" +
                    "<td>" + escapeHtml(f.TemplateName) + "</td>" +
                    linkCell(f) +
                    '<td><button type="button" class="cp-btn-action" data-row-action="convert">Convert</button> <span class="cp-status"></span></td>' +
                    "</tr>";
            });
    }

    function convertOneField(button, row) {
        return withSpinner(button, function () {
            return apiCall("convertField", { method: "POST", body: { databaseName: row.dataset.db, itemId: row.dataset.id } })
                .then(function (result) {
                    row.querySelector(".cp-status").innerHTML = statusPill(result.success ? "success" : "problem");
                    if (!result.success) {
                        showBanner(result.message);
                    }
                })
                .catch(function (err) { showBanner(err.message, err.details); });
        });
    }

    function convertAllFields(button) {
        return withSpinner(button, function () {
            return apiCall("convertAllFields", { method: "POST" })
                .then(function (result) {
                    if (result.Failures && result.Failures.length) {
                        showBanner("Converted " + result.Count + " field(s); " + result.Failures.length + " had a problem.", result.Failures.join("\n"));
                    }
                    return apiCall("scan");
                })
                .then(function (report) { state.report = report; renderStep1(); })
                .catch(function (err) { showBanner(err.message, err.details); });
        });
    }

    // ---- step 2: swatch-validator references ---------------------------------

    function scanStep2(button) {
        return withSpinner(button, function () {
            return apiCall("scan").then(function (report) {
                state.report = report;
                renderStep2();
            }).catch(function (err) { showBanner(err.message, err.details); });
        });
    }

    function renderStep2() {
        var rows = (state.report && state.report.SwatchValidatorFields) || [];
        var allButton = document.querySelector('[data-action="remove-all-links"]');
        renderTable("cp-table-2", "cp-empty-2", allButton, rows,
            ["Database", "Template", "Path", "Field"],
            function (f) {
                return "<tr data-db=\"" + escapeHtml(f.DatabaseName) + "\" data-id=\"" + escapeHtml(f.ItemId) +
                    "\" data-field=\"" + escapeHtml(f.ValidatorFieldId) + "\">" +
                    "<td>" + escapeHtml(f.DatabaseName) + "</td>" +
                    "<td>" + escapeHtml(f.TemplateName) + "</td>" +
                    linkCell(f) +
                    "<td>" + escapeHtml(f.ValidatorFieldName) + "</td>" +
                    '<td><button type="button" class="cp-btn-action" data-row-action="remove-link">Remove link</button> <span class="cp-status"></span></td>' +
                    "</tr>";
            });
    }

    function removeOneLink(button, row) {
        return withSpinner(button, function () {
            return apiCall("removeValidatorReference", {
                method: "POST",
                body: { databaseName: row.dataset.db, itemId: row.dataset.id, fieldId: row.dataset.field }
            }).then(function (result) {
                row.querySelector(".cp-status").innerHTML = statusPill(result.success ? "success" : "problem");
                if (!result.success) {
                    showBanner(result.message);
                }
            }).catch(function (err) { showBanner(err.message, err.details); });
        });
    }

    function removeAllLinks(button) {
        return withSpinner(button, function () {
            if (!window.confirm("Detach the swatch validator from every field listed here?")) {
                return;
            }
            return apiCall("removeAllValidatorReferences", { method: "POST" })
                .then(function (result) {
                    if (result.Failures && result.Failures.length) {
                        showBanner("Detached " + result.Count + " reference(s); " + result.Failures.length + " had a problem.", result.Failures.join("\n"));
                    }
                    return apiCall("scan");
                })
                .then(function (report) { state.report = report; renderStep2(); })
                .catch(function (err) { showBanner(err.message, err.details); });
        });
    }

    // ---- step 3: custom palettes ----------------------------------------------

    function scanStep3(button) {
        return withSpinner(button, function () {
            return apiCall("scan").then(function (report) {
                state.report = report;
                renderStep3();
            }).catch(function (err) { showBanner(err.message, err.details); });
        });
    }

    function renderStep3() {
        var rows = (state.report && state.report.CustomPalettes) || [];
        var allButton = document.querySelector('[data-action="remove-all-palettes"]');
        renderTable("cp-table-3", "cp-empty-3", allButton, rows,
            ["Database", "Path"],
            function (p) {
                return "<tr data-db=\"" + escapeHtml(p.DatabaseName) + "\" data-id=\"" + escapeHtml(p.ItemId) + "\">" +
                    "<td>" + escapeHtml(p.DatabaseName) + "</td>" +
                    linkCell({ ContentEditorUrl: p.ContentEditorUrl, ItemPath: p.ItemPath }) +
                    '<td><button type="button" class="cp-btn-action" data-row-action="remove-palette">Remove</button> <span class="cp-status"></span></td>' +
                    "</tr>";
            });
    }

    function removeOnePalette(button, row) {
        if (!window.confirm("Delete this Palette item? This cannot be undone.")) {
            return Promise.resolve();
        }
        return withSpinner(button, function () {
            return apiCall("removePaletteItem", { method: "POST", body: { databaseName: row.dataset.db, itemId: row.dataset.id } })
                .then(function (result) {
                    row.querySelector(".cp-status").innerHTML = statusPill(result.success ? "success" : "problem");
                    if (!result.success) {
                        showBanner(result.message);
                    }
                })
                .catch(function (err) { showBanner(err.message, err.details); });
        });
    }

    function removeAllPalettes(button) {
        if (!window.confirm("Delete every custom Palette item listed here? This cannot be undone.")) {
            return Promise.resolve();
        }
        return withSpinner(button, function () {
            return apiCall("removeAllPaletteItems", { method: "POST" })
                .then(function (result) {
                    if (result.Failures && result.Failures.length) {
                        showBanner("Removed " + result.Count + " item(s); " + result.Failures.length + " had a problem.", result.Failures.join("\n"));
                    }
                    return apiCall("scan");
                })
                .then(function (report) { state.report = report; renderStep3(); })
                .catch(function (err) { showBanner(err.message, err.details); });
        });
    }

    // ---- step 4: files to remove by hand ---------------------------------------

    function scanStep4(button) {
        return withSpinner(button, function () {
            return apiCall("files").then(renderFiles)
                .catch(function (err) { showBanner(err.message, err.details); });
        });
    }

    function renderFiles(files) {
        var html = files.length
            ? "<ul>" + files.map(function (f) { return "<li><code>" + escapeHtml(f) + "</code></li>"; }).join("") + "</ul>"
            : "<p class=\"cp-hint\">None found.</p>";

        document.getElementById("cp-files").innerHTML = html;
    }

    // ---- wiring -----------------------------------------------------------

    function onRowAction(table, handler) {
        table.addEventListener("click", function (e) {
            var button = e.target.closest("[data-row-action]");
            if (!button) {
                return;
            }
            handler(button, button.closest("tr"));
        });
    }

    document.addEventListener("DOMContentLoaded", function () {
        document.getElementById("cp-start").addEventListener("click", function () { goTo("1"); });

        Array.prototype.forEach.call(document.querySelectorAll("[data-next]"), function (btn) {
            btn.addEventListener("click", function () { goTo(btn.getAttribute("data-next")); });
        });

        Array.prototype.forEach.call(document.querySelectorAll("[data-prev]"), function (btn) {
            btn.addEventListener("click", function () { goTo(btn.getAttribute("data-prev")); });
        });

        document.querySelector('[data-action="scan-1"]').addEventListener("click", function (e) { scanStep1(e.currentTarget); });
        document.querySelector('[data-action="convert-all"]').addEventListener("click", function (e) { convertAllFields(e.currentTarget); });
        onRowAction(document.getElementById("cp-table-1"), function (btn, row) { convertOneField(btn, row); });

        document.querySelector('[data-action="scan-2"]').addEventListener("click", function (e) { scanStep2(e.currentTarget); });
        document.querySelector('[data-action="remove-all-links"]').addEventListener("click", function (e) { removeAllLinks(e.currentTarget); });
        onRowAction(document.getElementById("cp-table-2"), function (btn, row) { removeOneLink(btn, row); });

        document.querySelector('[data-action="scan-3"]').addEventListener("click", function (e) { scanStep3(e.currentTarget); });
        document.querySelector('[data-action="remove-all-palettes"]').addEventListener("click", function (e) { removeAllPalettes(e.currentTarget); });
        onRowAction(document.getElementById("cp-table-3"), function (btn, row) { removeOnePalette(btn, row); });

        document.querySelector('[data-action="scan-4"]').addEventListener("click", function (e) { scanStep4(e.currentTarget); });
    });
})();
