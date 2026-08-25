<%@ Page Language="C#" AutoEventWireup="true" Inherits="SoftConsulting.ColorPicker.Admin.ColorPickerUninstallPage" %>
<!DOCTYPE html>
<html>
<head runat="server">
    <title>Uninstall Color Picker</title>
    <link rel="stylesheet" href="Uninstall.css" />
</head>
<body>
    <script>window.ColorPickerToken = "<%= Token %>";</script>

    <div class="cp-page">
        <h1>Uninstall Color Picker</h1>
        <p class="cp-hint">
            Scans for anything that would make it unsafe to remove the SoftConsulting.ColorPicker
            module, and removes its Sitecore items once it is safe to do so.
        </p>

        <ol class="cp-steps" id="cp-steps">
            <li data-step="welcome" class="active">Start</li>
            <li data-step="1">Color Picker fields</li>
            <li data-step="2">Validator references</li>
            <li data-step="3">Custom palettes</li>
            <li data-step="4">Remove module</li>
        </ol>

        <div id="cp-banner" class="cp-banner" hidden>
            <div class="cp-banner-headline"></div>
            <button type="button" class="cp-banner-toggle" hidden>Details</button>
            <pre class="cp-banner-details" hidden></pre>
        </div>

        <section id="cp-panel-welcome" class="cp-panel active">
            <p class="cp-hint">Four steps, each with its own scan and fix. Nothing is changed until you act on it.</p>
            <button type="button" class="cp-btn-wizard" id="cp-start">Start</button>
        </section>

        <section id="cp-panel-1" class="cp-panel">
            <h2>Step 1 - Template fields typed "Color Picker"</h2>
            <p class="cp-hint">
                Fields still typed <strong>Color Picker</strong> - convert them to Single-Line Text
                (lossless, since the field only ever stores a plain hex string).
            </p>
            <div class="cp-panel-actions">
                <button type="button" class="cp-btn-action" data-action="scan-1">Scan</button>
                <button type="button" class="cp-btn-action" data-action="convert-all" hidden>Convert all</button>
            </div>
            <div class="cp-table-wrap"><table id="cp-table-1"><tbody></tbody></table></div>
            <p class="cp-empty" id="cp-empty-1" hidden>None - safe to remove the field type.</p>
            <button type="button" class="cp-btn-wizard cp-btn-secondary" data-prev="welcome">Back</button>
            <button type="button" class="cp-btn-wizard" data-next="2">Next</button>
        </section>

        <section id="cp-panel-2" class="cp-panel">
            <h2>Step 2 - Fields using the "Color Picker Swatch" validator</h2>
            <p class="cp-hint">
                Fields whose Validators list references the <strong>Color Picker Swatch</strong> validator.
                Informational, not blocking - the validator item is removed in step 4 regardless.
                Detaching references here just avoids leaving a field with a dangling one. If this
                looks incomplete, the Link Database may be stale - rebuild it via Control Panel &rarr;
                Indexing &rarr; Rebuild Link Database, then re-scan.
            </p>
            <div class="cp-panel-actions">
                <button type="button" class="cp-btn-action" data-action="scan-2">Scan</button>
                <button type="button" class="cp-btn-action" data-action="remove-all-links" hidden>Remove all</button>
            </div>
            <div class="cp-table-wrap"><table id="cp-table-2"><tbody></tbody></table></div>
            <p class="cp-empty" id="cp-empty-2" hidden>None.</p>
            <button type="button" class="cp-btn-wizard cp-btn-secondary" data-prev="1">Back</button>
            <button type="button" class="cp-btn-wizard" data-next="3">Next</button>
        </section>

        <section id="cp-panel-3" class="cp-panel">
            <h2>Step 3 - Custom Palette items</h2>
            <p class="cp-hint">
                Items based on the Palette template other than the two seeded samples. Removing
                them here lets the Palette template be removed too in step 4; leaving any in place
                keeps the template around so they aren't orphaned.
            </p>
            <div class="cp-panel-actions">
                <button type="button" class="cp-btn-action" data-action="scan-3">Scan</button>
                <button type="button" class="cp-btn-action" data-action="remove-all-palettes" hidden>Remove all</button>
            </div>
            <div class="cp-table-wrap"><table id="cp-table-3"><tbody></tbody></table></div>
            <p class="cp-empty" id="cp-empty-3" hidden>None - safe to remove the Palette template.</p>
            <button type="button" class="cp-btn-wizard cp-btn-secondary" data-prev="2">Back</button>
            <button type="button" class="cp-btn-wizard" data-next="4">Next</button>
        </section>

        <section id="cp-panel-4" class="cp-panel">
            <h2>Step 4 - Remove module</h2>
            <p class="cp-hint">
                Preview the module's own Sitecore items below, then remove them. A short list of files
                still needs to be deleted by hand afterward - they're loaded by this application, so removal
                can't touch them - and that list appears once removal finishes.
            </p>
            <div class="cp-panel-actions">
                <button type="button" class="cp-btn-action" data-action="scan-4">Preview</button>
            </div>
            <div id="cp-manifest"></div>
            <button type="button" class="cp-btn-wizard cp-btn-secondary" data-prev="3">Back</button>
            <button type="button" class="cp-btn-wizard cp-btn-danger" id="cp-remove">Remove module</button>
            <div id="cp-final"></div>
        </section>
    </div>

    <script src="Uninstall.js"></script>
</body>
</html>
