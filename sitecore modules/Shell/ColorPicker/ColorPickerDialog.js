/*
  Color picker dialog behavior (ColorPickerDialog.xml + Views/ColorPicker/Dialog.cshtml).

  The color is kept as HSV + alpha (h 0-360, s/v 0-1, a 0-255) so hue survives while dragging
  through grays/black; every other representation (RGB, CMYK, hex) is derived from it. The picked
  value is written to the Sheer input named by data-cp-value-input ("" while nothing is picked),
  which ColorPickerDialogForm reads and validates when OK is pressed.

  Sheer dialog pages load Prototype.js, which patches built-ins: this file avoids
  Function.prototype.bind and JSON (Prototype's Array#toJSON breaks JSON.stringify of arrays).
*/
(function () {
  'use strict';

  var RECENT_STORAGE_KEY = 'SoftConsulting.ColorPicker.RecentColors';
  var RECENT_MAX = 24;
  var EMPTY_LABEL = '–';

  // ---- Color math ---------------------------------------------------------------------------

  function clamp(value, min, max) {
    return Math.min(max, Math.max(min, value));
  }

  function hsvToRgb(h, s, v) {
    var c = v * s;
    var hp = (h % 360) / 60;
    var x = c * (1 - Math.abs((hp % 2) - 1));
    var m = v - c;
    var r = 0, g = 0, b = 0;
    if (hp < 1) { r = c; g = x; }
    else if (hp < 2) { r = x; g = c; }
    else if (hp < 3) { g = c; b = x; }
    else if (hp < 4) { g = x; b = c; }
    else if (hp < 5) { r = x; b = c; }
    else { r = c; b = x; }
    return {
      r: Math.round((r + m) * 255),
      g: Math.round((g + m) * 255),
      b: Math.round((b + m) * 255)
    };
  }

  function rgbToHsv(r, g, b) {
    r /= 255; g /= 255; b /= 255;
    var max = Math.max(r, g, b);
    var min = Math.min(r, g, b);
    var d = max - min;
    var h = 0;
    if (d !== 0) {
      if (max === r) { h = 60 * (((g - b) / d) % 6); }
      else if (max === g) { h = 60 * ((b - r) / d + 2); }
      else { h = 60 * ((r - g) / d + 4); }
    }
    if (h < 0) { h += 360; }
    return { h: h, s: max === 0 ? 0 : d / max, v: max };
  }

  function rgbToCmyk(rgb) {
    var r = rgb.r / 255, g = rgb.g / 255, b = rgb.b / 255;
    var k = 1 - Math.max(r, g, b);
    if (k >= 1) { return { c: 0, m: 0, y: 0, k: 100 }; }
    return {
      c: (1 - r - k) / (1 - k) * 100,
      m: (1 - g - k) / (1 - k) * 100,
      y: (1 - b - k) / (1 - k) * 100,
      k: k * 100
    };
  }

  function cmykToRgb(c, m, y, k) {
    c /= 100; m /= 100; y /= 100; k /= 100;
    return {
      r: Math.round(255 * (1 - c) * (1 - k)),
      g: Math.round(255 * (1 - m) * (1 - k)),
      b: Math.round(255 * (1 - y) * (1 - k))
    };
  }

  function hex2(n) {
    var s = Math.round(n).toString(16).toUpperCase();
    return s.length < 2 ? '0' + s : s;
  }

  // Accepts "RRGGBB"/"#RRGGBB", and "RRGGBBAA"/"#RRGGBBAA" when allowAlpha. Returns null when invalid.
  function parseHex(value, allowAlpha) {
    var v = (value || '').trim();
    if (v.charAt(0) !== '#') { v = '#' + v; }
    var pattern = allowAlpha ? /^#[0-9A-Fa-f]{6}([0-9A-Fa-f]{2})?$/ : /^#[0-9A-Fa-f]{6}$/;
    if (!pattern.test(v)) { return null; }
    return {
      r: parseInt(v.substr(1, 2), 16),
      g: parseInt(v.substr(3, 2), 16),
      b: parseInt(v.substr(5, 2), 16),
      a: v.length === 9 ? parseInt(v.substr(7, 2), 16) : 255
    };
  }

  function cssColor(rgb, a) {
    return 'rgba(' + rgb.r + ',' + rgb.g + ',' + rgb.b + ',' + (a / 255).toFixed(3) + ')';
  }

  function round1(n) {
    return Math.round(n * 10) / 10;
  }

  // ---- Browser storage (best effort: may be blocked or unavailable) ---------------------------

  function loadRecent() {
    try {
      var raw = window.localStorage.getItem(RECENT_STORAGE_KEY);
      return raw ? raw.split(',').filter(function (h) { return /^#[0-9A-F]{6}([0-9A-F]{2})?$/.test(h); }) : [];
    } catch (e) {
      return [];
    }
  }

  function storeRecent(list) {
    try {
      window.localStorage.setItem(RECENT_STORAGE_KEY, list.join(','));
    } catch (e) {
      // Ignore - recent colors are a convenience only.
    }
  }

  // ---- Picker -------------------------------------------------------------------------------

  function Picker(root) {
    var self = this;
    this.root = root;
    this.allowAlpha = root.getAttribute('data-cp-allow-transparency') === 'true';
    this.allowCustom = root.getAttribute('data-cp-allow-custom') === 'true';
    this.valueInput = document.getElementById(root.getAttribute('data-cp-value-input'));
    this.original = root.getAttribute('data-cp-original') || '';

    this.sv = this.find('[data-cp-sv]');
    this.svCursor = this.find('[data-cp-sv-cursor]');
    this.hue = this.find('[data-cp-hue]');
    this.hueThumb = this.find('[data-cp-hue-thumb]');
    this.alpha = this.find('[data-cp-alpha]');
    this.alphaFill = this.find('[data-cp-alpha-fill]');
    this.alphaThumb = this.find('[data-cp-alpha-thumb]');
    this.newChip = this.find('[data-cp-new] span');
    this.currentChip = this.find('[data-cp-current]');
    this.nameLabel = this.find('[data-cp-name-label]');
    this.hexLabel = this.find('[data-cp-hex-label]');
    this.recentPanel = this.find('[data-cp-panel="recent"]');

    this.inputs = {};
    var inputs = root.querySelectorAll('[data-cp-input]');
    for (var i = 0; i < inputs.length; i++) {
      this.inputs[inputs[i].getAttribute('data-cp-input')] = inputs[i];
    }

    this.palette = {};
    var swatches = root.querySelectorAll('[data-cp-panel="palette"] [data-cp-swatch]');
    for (var j = 0; j < swatches.length; j++) {
      this.palette[swatches[j].getAttribute('data-cp-swatch')] = swatches[j].getAttribute('data-cp-name');
    }

    this.state = { h: 0, s: 0, v: 0, a: 255 };
    this.hasColor = false;
    var initial = parseHex(this.original, this.allowAlpha);
    if (initial) {
      this.setRgb(initial, initial.a);
      this.hasColor = true;
    }

    this.paintChip(this.currentChip.firstElementChild, initial);
    this.currentChip.disabled = !initial;

    this.bindSpectrum();
    this.bindInputs();
    this.bindSwatches();
    this.bindActions();
    this.renderRecent();
    this.render();

    // Remember the color when the dialog is confirmed (OK button or Enter, see FormDialog.xml).
    document.addEventListener('click', function (e) {
      if (e.target && e.target.closest && e.target.closest('#OK')) { self.rememberColor(); }
    }, true);
    document.addEventListener('keydown', function (e) {
      if (e.keyCode === 13) { self.rememberColor(); }
    }, true);
  }

  Picker.prototype.find = function (selector) {
    return this.root.querySelector(selector);
  };

  Picker.prototype.rgb = function () {
    return hsvToRgb(this.state.h, this.state.s, this.state.v);
  };

  Picker.prototype.hex = function () {
    var rgb = this.rgb();
    var value = '#' + hex2(rgb.r) + hex2(rgb.g) + hex2(rgb.b);
    return this.allowAlpha && this.state.a < 255 ? value + hex2(this.state.a) : value;
  };

  // Sets the color from RGB, keeping the current hue (and saturation) where RGB can't express
  // it - for grays and black - so dragging through them doesn't snap the spectrum back to red.
  Picker.prototype.setRgb = function (rgb, a) {
    var hsv = rgbToHsv(rgb.r, rgb.g, rgb.b);
    if (hsv.s > 0 && hsv.v > 0) { this.state.h = hsv.h; }
    if (hsv.v > 0) { this.state.s = hsv.s; }
    this.state.v = hsv.v;
    if (typeof a === 'number') { this.state.a = this.allowAlpha ? clamp(a, 0, 255) : 255; }
    this.hasColor = true;
  };

  Picker.prototype.setHex = function (value) {
    var parsed = parseHex(value, true);
    if (!parsed) { return false; }
    this.setRgb(parsed, parsed.a);
    return true;
  };

  // Redraws everything from the state. "except" names the input being typed into, which is left
  // alone so re-formatting never fights the cursor.
  Picker.prototype.render = function (except) {
    var rgb = this.rgb();
    var hex = this.hasColor ? this.hex() : '';
    var a = this.state.a;

    if (this.sv) {
      this.sv.style.backgroundColor = 'hsl(' + Math.round(this.state.h) + ',100%,50%)';
      this.svCursor.style.left = (this.state.s * 100) + '%';
      this.svCursor.style.top = ((1 - this.state.v) * 100) + '%';
      this.sv.setAttribute('aria-valuetext', 'Saturation ' + Math.round(this.state.s * 100) + '%, brightness ' + Math.round(this.state.v * 100) + '%');
    }
    if (this.hue) {
      this.hueThumb.style.top = ((1 - this.state.h / 360) * 100) + '%';
      this.hue.setAttribute('aria-valuenow', Math.round(this.state.h) % 360);
    }
    if (this.alpha) {
      this.alphaFill.style.background = 'linear-gradient(to bottom, ' + cssColor(rgb, 255) + ', ' + cssColor(rgb, 0) + ')';
      this.alphaThumb.style.top = ((1 - a / 255) * 100) + '%';
      this.alpha.setAttribute('aria-valuenow', a);
    }

    var cmyk = rgbToCmyk(rgb);
    var values = {
      r: rgb.r, g: rgb.g, b: rgb.b,
      h: Math.round(this.state.h) % 360,
      s: Math.round(this.state.s * 100),
      v: Math.round(this.state.v * 100),
      c: round1(cmyk.c), m: round1(cmyk.m), y: round1(cmyk.y), k: round1(cmyk.k),
      a: a,
      hex: hex
    };
    for (var key in values) {
      if (values.hasOwnProperty(key) && this.inputs[key] && key !== except) {
        this.inputs[key].value = this.hasColor || key === 'hex' ? values[key] : '';
        this.inputs[key].classList.remove('cp-invalid');
      }
    }

    this.paintChip(this.newChip, this.hasColor ? { r: rgb.r, g: rgb.g, b: rgb.b, a: this.allowAlpha ? a : 255 } : null);

    var name = this.palette[hex];
    this.nameLabel.textContent = name || EMPTY_LABEL;
    if (this.hexLabel) { this.hexLabel.textContent = hex || EMPTY_LABEL; }

    var swatches = this.root.querySelectorAll('[data-cp-swatch]');
    for (var i = 0; i < swatches.length; i++) {
      swatches[i].classList.toggle('cp-selected', swatches[i].getAttribute('data-cp-swatch') === hex);
    }

    if (this.valueInput) { this.valueInput.value = hex; }
  };

  Picker.prototype.paintChip = function (el, color) {
    if (!el) { return; }
    el.style.background = color ? cssColor(color, color.a) : 'transparent';
  };

  // ---- Spectrum interaction -----------------------------------------------------------------

  Picker.prototype.bindSpectrum = function () {
    var self = this;

    this.drag(this.sv, function (x, y) {
      self.state.s = x;
      self.state.v = 1 - y;
    });
    this.drag(this.hue, function (x, y) {
      self.state.h = (1 - y) * 360;
    });
    this.drag(this.alpha, function (x, y) {
      self.state.a = Math.round((1 - y) * 255);
    });

    this.keys(this.sv, function (dx, dy) {
      self.state.s = clamp(self.state.s + dx / 100, 0, 1);
      self.state.v = clamp(self.state.v - dy / 100, 0, 1);
    });
    this.keys(this.hue, function (dx, dy) {
      self.state.h = clamp(self.state.h - dy, 0, 360);
    });
    this.keys(this.alpha, function (dx, dy) {
      self.state.a = clamp(self.state.a - dy, 0, 255);
    });
  };

  // Calls update(x, y) with the pointer position inside el, both 0-1, while dragging.
  Picker.prototype.drag = function (el, update) {
    if (!el) { return; }
    var self = this;

    function apply(e) {
      var rect = el.getBoundingClientRect();
      update(
        clamp((e.clientX - rect.left) / rect.width, 0, 1),
        clamp((e.clientY - rect.top) / rect.height, 0, 1));
      self.hasColor = true;
      self.render();
    }

    el.addEventListener('pointerdown', function (e) {
      if (e.button !== 0) { return; }
      e.preventDefault();
      el.focus();
      el.setPointerCapture(e.pointerId);
      apply(e);

      function move(ev) { apply(ev); }
      function end() {
        el.removeEventListener('pointermove', move);
        el.removeEventListener('pointerup', end);
        el.removeEventListener('pointercancel', end);
      }
      el.addEventListener('pointermove', move);
      el.addEventListener('pointerup', end);
      el.addEventListener('pointercancel', end);
    });
  };

  // Arrow keys nudge by 1 (10 with Shift); update(dx, dy) follows screen directions.
  Picker.prototype.keys = function (el, update) {
    if (!el) { return; }
    var self = this;
    var deltas = { 37: [-1, 0], 38: [0, -1], 39: [1, 0], 40: [0, 1] };

    el.addEventListener('keydown', function (e) {
      var d = deltas[e.keyCode];
      if (!d) { return; }
      e.preventDefault();
      var step = e.shiftKey ? 10 : 1;
      update(d[0] * step, d[1] * step);
      self.hasColor = true;
      self.render();
    });
  };

  // ---- Value fields -------------------------------------------------------------------------

  Picker.prototype.bindInputs = function () {
    var self = this;

    function onInput(key, input) {
      return function () {
        var ok = self.applyInput(key, input.value);
        input.classList.toggle('cp-invalid', !ok);
        if (ok) { self.render(key); }
      };
    }

    function onCommit() {
      // Re-format/restore once the author leaves the field (or presses Enter).
      self.render();
    }

    for (var key in this.inputs) {
      if (this.inputs.hasOwnProperty(key)) {
        this.inputs[key].addEventListener('input', onInput(key, this.inputs[key]));
        this.inputs[key].addEventListener('change', onCommit);
        this.inputs[key].addEventListener('blur', onCommit);
      }
    }
  };

  // Applies one field's typed value to the state; returns false when the value isn't valid.
  Picker.prototype.applyInput = function (key, raw) {
    if (key === 'hex') {
      if (!parseHex(raw, this.allowAlpha)) { return false; }
      return this.setHex(raw);
    }

    var n = parseFloat(raw);
    if (isNaN(n)) { return false; }

    var rgb = this.rgb();
    switch (key) {
      case 'r': case 'g': case 'b':
        rgb[key] = clamp(Math.round(n), 0, 255);
        this.setRgb(rgb);
        return true;
      case 'h':
        this.state.h = clamp(n, 0, 359);
        break;
      case 's':
        this.state.s = clamp(n, 0, 100) / 100;
        break;
      case 'v':
        this.state.v = clamp(n, 0, 100) / 100;
        break;
      case 'a':
        this.state.a = clamp(Math.round(n), 0, 255);
        break;
      case 'c': case 'm': case 'y': case 'k':
        var cmyk = rgbToCmyk(rgb);
        cmyk[key] = clamp(n, 0, 100);
        this.setRgb(cmykToRgb(cmyk.c, cmyk.m, cmyk.y, cmyk.k));
        return true;
      default:
        return false;
    }

    this.hasColor = true;
    return true;
  };

  // ---- Swatches & recent colors -------------------------------------------------------------

  Picker.prototype.bindSwatches = function () {
    var self = this;

    this.root.addEventListener('click', function (e) {
      var swatch = e.target.closest ? e.target.closest('[data-cp-swatch]') : null;
      if (swatch && self.setHex(swatch.getAttribute('data-cp-swatch'))) {
        self.render();
      }
    });

    // Double-clicking a swatch picks it and confirms the dialog in one go.
    this.root.addEventListener('dblclick', function (e) {
      var swatch = e.target.closest ? e.target.closest('[data-cp-swatch]') : null;
      var ok = document.getElementById('OK');
      if (swatch && ok && self.setHex(swatch.getAttribute('data-cp-swatch'))) {
        self.render();
        ok.click();
      }
    });

    var tabs = this.root.querySelectorAll('[data-cp-tab]');
    for (var i = 0; i < tabs.length; i++) {
      tabs[i].addEventListener('change', function () {
        var panels = self.root.querySelectorAll('[data-cp-panel]');
        for (var p = 0; p < panels.length; p++) {
          panels[p].hidden = panels[p].getAttribute('data-cp-panel') !== this.value;
        }
      });
    }

    this.currentChip.addEventListener('click', function () {
      if (self.setHex(self.original)) { self.render(); }
    });
  };

  Picker.prototype.recentColors = function () {
    var self = this;
    var seen = {};
    return loadRecent()
      .map(function (h) { return self.allowAlpha ? h : h.substr(0, 7); })
      .filter(function (h) { return seen[h] ? false : (seen[h] = true); });
  };

  Picker.prototype.renderRecent = function () {
    if (!this.recentPanel) { return; }

    var colors = this.recentColors();
    var old = this.recentPanel.querySelectorAll('[data-cp-swatch]');
    for (var i = 0; i < old.length; i++) { old[i].parentNode.removeChild(old[i]); }
    this.find('[data-cp-recent-empty]').hidden = colors.length > 0;

    for (var j = 0; j < colors.length; j++) {
      var button = document.createElement('button');
      var fill = document.createElement('span');
      button.type = 'button';
      button.className = 'cp-swatch';
      button.title = colors[j];
      button.setAttribute('aria-label', colors[j]);
      button.setAttribute('data-cp-swatch', colors[j]);
      fill.style.background = colors[j];
      button.appendChild(fill);
      this.recentPanel.appendChild(button);
    }
  };

  Picker.prototype.rememberColor = function () {
    if (!this.allowCustom || !this.hasColor) { return; }
    var hex = this.hex();
    var list = loadRecent().filter(function (h) { return h !== hex; });
    list.unshift(hex);
    storeRecent(list.slice(0, RECENT_MAX));
  };

  // ---- Eyedropper & copy --------------------------------------------------------------------

  Picker.prototype.bindActions = function () {
    var self = this;

    var eyedropper = this.find('[data-cp-eyedropper]');
    if (eyedropper && typeof window.EyeDropper === 'function') {
      eyedropper.hidden = false;
      eyedropper.addEventListener('click', function () {
        new window.EyeDropper().open().then(function (result) {
          // Screen pixels are opaque - keep whatever alpha is already set.
          var alpha = self.state.a;
          if (self.setHex(result.sRGBHex)) {
            self.state.a = alpha;
            self.render();
          }
        }, function () {
          // Cancelled with Esc, or not permitted in this frame - nothing to do.
        });
      });
    }

    var copy = this.find('[data-cp-copy]');
    if (copy) {
      copy.addEventListener('click', function () {
        if (!self.hasColor) { return; }
        self.copyText(self.hex(), copy);
      });
    }
  };

  Picker.prototype.copyText = function (text, button) {
    function done() {
      var title = button.title;
      button.title = 'Copied ' + text;
      button.classList.add('cp-copied');
      window.setTimeout(function () {
        button.title = title;
        button.classList.remove('cp-copied');
      }, 1500);
    }

    function fallback() {
      var area = document.createElement('textarea');
      area.value = text;
      area.style.position = 'fixed';
      area.style.opacity = '0';
      document.body.appendChild(area);
      area.select();
      try { document.execCommand('copy'); done(); } catch (e) { /* ignore */ }
      document.body.removeChild(area);
    }

    if (navigator.clipboard && navigator.clipboard.writeText) {
      navigator.clipboard.writeText(text).then(done, fallback);
    } else {
      fallback();
    }
  };

  // ---- Fit the dialog frame to its content and the visible window ----------------------------
  //
  // Sitecore's modal host (jQueryModalDialogs.html) opens the dialog at the fixed size the
  // command requested. That size is only a first guess: the content's real height depends on the
  // field's Source (transparency, palette-only mode, palette size) and on the swatch tab shown,
  // so a fixed height leaves either an empty band above the buttons or a needless scrollbar. And
  // the host only shrinks a dialog when BOTH dimensions exceed 80% of the window, so on a
  // zoomed-in page the frame easily ends up taller than the window, pushing OK/Cancel out of
  // view behind the outer page's scrollbar.
  //
  // So the frame is sized to exactly what the content needs, capped by the window - on load,
  // whenever the content's size changes, and whenever the window is resized (zooming triggers
  // that too):
  // - Width: the layout first gets the full available width (the requested width, capped by the
  //   window) so its two columns sit side by side whenever they fit, then the frame shrinks to
  //   the width the layout actually uses - just the spectrum column once the value fields have
  //   wrapped underneath it on a zoomed-in page.
  // - Height: what the content needs at that width, capped by the window.
  // Sizes are measured, not assumed: after each resize the frame is checked and corrected, so the
  // host's own title bar/borders can't leave a needless scrollbar. Inside the frame only the
  // content area scrolls (see ColorPickerDialog.css), so the header and the OK/Cancel footer
  // always stay visible.

  var FIT_MARGIN = 40;
  var FIT_MIN_WIDTH = 320;
  var FIT_MIN_HEIGHT = 280;
  // Room for sub-pixel rounding, so a fitting layout never shows a scrollbar.
  var FIT_SLACK = 2;
  // The host adds this much for its own title bar on top of the height it is given; any
  // difference is measured and corrected in setOuterHeight.
  var HOST_HEADER_HEIGHT = 15;

  function DialogFitter(content) {
    this.content = content;
    this.record = null;
    this.preferredWidth = 0;
  }

  DialogFitter.prototype.start = function () {
    var self = this;
    try {
      var dialogs = window.top._scDialogs;
      // The host keeps the most recently opened dialog first - find the one hosting this page.
      for (var i = 0; dialogs && i < dialogs.length; i++) {
        if (dialogs[i].contentIframe && dialogs[i].contentIframe[0] &&
            dialogs[i].contentIframe[0].contentWindow === window) {
          this.record = dialogs[i];
          break;
        }
      }
    } catch (e) {
      return; // Not hosted by Sitecore's modal dialog host (or cross-origin) - nothing to fit.
    }
    if (!this.record || i !== 0 || typeof scForm === 'undefined' || !scForm.setDialogDimension) {
      // setDialogDimension always resizes the top-most dialog, so only fit when that's us.
      return;
    }

    this.scroller = this.content.closest('.scDialogContentContainer');
    if (!this.scroller) { return; }

    this.preferredWidth = this.record.contentIframe.dialog('widget').outerWidth();

    function schedule() {
      if (self.timer) { window.clearTimeout(self.timer); }
      self.timer = window.setTimeout(function () { self.fit(); }, 50);
    }

    window.top.addEventListener('resize', schedule);
    window.addEventListener('unload', function () {
      try { window.top.removeEventListener('resize', schedule); } catch (e) { /* ignore */ }
    });

    // Content height changes: switching the Palette/Recent tab, a wrapping layout after a width
    // change, etc.
    if (typeof window.ResizeObserver === 'function') {
      new window.ResizeObserver(schedule).observe(this.content);
    }

    this.fit();
  };

  function horizontalPadding(el) {
    var style = window.getComputedStyle(el);
    return parseFloat(style.paddingLeft) + parseFloat(style.paddingRight);
  }

  function verticalPadding(el) {
    var style = window.getComputedStyle(el);
    return parseFloat(style.paddingTop) + parseFloat(style.paddingBottom);
  }

  // The width the layout actually occupies: the right edge of its furthest-reaching column.
  // When the columns wrap onto separate rows this is just the widest one, not the full row.
  DialogFitter.prototype.usedContentWidth = function () {
    var layout = this.content.querySelector('.cp-layout');
    if (!layout) { return this.content.scrollWidth; }

    var left = layout.getBoundingClientRect().left;
    var used = 0;
    for (var i = 0; i < layout.children.length; i++) {
      var rect = layout.children[i].getBoundingClientRect();
      if (rect.width > 0) { used = Math.max(used, rect.right - left); }
    }
    return used + horizontalPadding(this.content);
  };

  // Frame width at which the content fits without empty space: everything around the content
  // area (scrollbar excluded - see fit) plus the content's padding and used width.
  DialogFitter.prototype.neededWidth = function (widget) {
    var around = widget.outerWidth() - this.scroller.offsetWidth;
    return Math.ceil(around + horizontalPadding(this.scroller) + this.usedContentWidth()) + FIT_SLACK;
  };

  // Frame height at which the content fits exactly: everything around the content area (host
  // title bar, dialog header, footer) plus the content's padding and height, including anything
  // overhanging it (e.g. slider thumbs).
  DialogFitter.prototype.neededHeight = function (widget) {
    var around = widget.outerHeight() - this.scroller.clientHeight;
    var content = Math.max(this.content.offsetHeight, this.content.scrollHeight);
    return Math.ceil(around + verticalPadding(this.scroller) + content) + FIT_SLACK;
  };

  DialogFitter.prototype.setOuterWidth = function (widget, width) {
    if (Math.abs(widget.outerWidth() - width) < 1) { return; }
    scForm.setDialogDimension(width, null);
    var error = width - widget.outerWidth();
    if (Math.abs(error) >= 1) { scForm.setDialogDimension(width + error, null); }
  };

  DialogFitter.prototype.setOuterHeight = function (widget, height) {
    if (Math.abs(widget.outerHeight() - height) < 1) { return; }
    var requested = height - HOST_HEADER_HEIGHT;
    scForm.setDialogDimension(null, requested);
    var error = height - widget.outerHeight();
    if (Math.abs(error) >= 1) { scForm.setDialogDimension(null, requested + error); }
  };

  DialogFitter.prototype.fit = function () {
    try {
      var top = window.top;
      var widget = this.record.contentIframe.dialog('widget');
      var maxWidth = Math.max(FIT_MIN_WIDTH, Math.min(this.preferredWidth, top.innerWidth - FIT_MARGIN));
      var maxHeight = Math.max(FIT_MIN_HEIGHT, top.innerHeight - FIT_MARGIN);

      // 1. Let the layout spread out over all the width it may have, then shrink to what it uses.
      this.setOuterWidth(widget, maxWidth);
      var width = Math.max(FIT_MIN_WIDTH, Math.min(maxWidth, this.neededWidth(widget)));
      this.setOuterWidth(widget, width);

      // 2. Height for the content at that width.
      this.setOuterHeight(widget, Math.max(FIT_MIN_HEIGHT, Math.min(maxHeight, this.neededHeight(widget))));

      // 3. Capped by the window, the content scrolls - make room for the scrollbar so it doesn't
      //    squeeze the content or add a horizontal scrollbar too.
      var scrollbar = this.scroller.offsetWidth - this.scroller.clientWidth;
      if (scrollbar > 0) {
        this.setOuterWidth(widget, Math.min(maxWidth, width + scrollbar));
      }

      // Re-center: jQuery UI keeps the old top/left when the size changes.
      widget.css({
        left: Math.max(0, (top.innerWidth - widget.outerWidth()) / 2) + 'px',
        top: Math.max(0, (top.innerHeight - widget.outerHeight()) / 2) + 'px'
      });
    } catch (e) {
      // Best effort only - the dialog still works at its original size.
    }
  };

  // ---- Bootstrap ----------------------------------------------------------------------------

  function init() {
    var root = document.querySelector('[data-cp-root]');
    if (!root || root.getAttribute('data-cp-ready')) { return; }
    root.setAttribute('data-cp-ready', 'true');
    new Picker(root);

    // Measure only after the host has finished setting the frame up (it does so on load).
    var fitter = new DialogFitter(root);
    if (document.readyState === 'complete') {
      fitter.start();
    } else {
      window.addEventListener('load', function () { fitter.start(); });
    }
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init);
  } else {
    init();
  }
  window.addEventListener('load', init);
})();
