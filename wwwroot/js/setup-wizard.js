var wizard = wizard || {};

(function () {
    'use strict';

    var currentStep = 0;
    var totalSteps = 3;
    var selectedLanguage = null;
    var selectedTheme = 'system';
    var availableLanguages = [];
    var noLanguageFiles = false;

    // Initialize
    wizard.init = function () {
        currentStep = 0;
        selectedLanguage = null;
        selectedTheme = 'system';
        updateStepVisibility();
        updateProgressDots();
        updateButtons();
        loadLanguages();
    };

    // Load available languages from API
    function loadLanguages() {
        Promise.all([
            fetch('/api/localization/languages').then(function (r) { return r.json(); }),
            fetch('/api/setup/detectLanguage').then(function (r) { return r.json(); })
        ]).then(function (results) {
            var langResult = results[0];
            var detectResult = results[1];

            var langList = document.getElementById('languageList');
            var detectionDiv = document.getElementById('systemLanguageDetection');
            var noFilesWarning = document.getElementById('noLanguageWarning');

            if (!langResult.success || !langResult.data || langResult.data.length === 0) {
                noLanguageFiles = true;
                if (noFilesWarning) noFilesWarning.style.display = 'block';
                if (langList) langList.innerHTML = '<div style="color:#666; font-size:13px; padding:12px;">@Localizer["SetupWizard.NoLanguageFiles"]</div>';
                if (detectionDiv) detectionDiv.textContent = 'No language files found.';
                return;
            }

            availableLanguages = langResult.data;

            // Build language list
            if (langList) {
                var html = '';
                langResult.data.forEach(function (lang) {
                    var selected = lang.code === (detectResult.data && detectResult.data.detectedLanguage) ? 'style="border-color:#3B82F6; background:#1a2a4a;"' : '';
                    html += '<label ' + selected + ' class="wizard-lang-option" data-code="' + lang.code + '" style="display:flex; align-items:center; gap:12px; padding:12px 16px; background:#0d0d0d; border:1px solid #333; border-radius:8px; cursor:pointer; transition:all 0.2s;">';
                    html += '<input type="radio" name="language" value="' + lang.code + '" style="accent-color:#3B82F6;" />';
                    html += '<div style="flex:1;">';
                    html += '<div style="color:#ddd; font-size:14px;">' + escapeHtml(lang.nativeName || lang.name || lang.code) + '</div>';
                    html += '<div style="color:#666; font-size:11px;">' + escapeHtml(lang.name || lang.code) + '</div>';
                    html += '</div>';
                    html += '</label>';
                });
                langList.innerHTML = html;

                // Bind click events
                var options = langList.querySelectorAll('.wizard-lang-option');
                options.forEach(function (opt) {
                    opt.addEventListener('click', function () {
                        var code = this.getAttribute('data-code');
                        selectedLanguage = code;
                        // Update visual selection
                        options.forEach(function (o) {
                            o.style.borderColor = '#333';
                            o.style.background = '#0d0d0d';
                        });
                        this.style.borderColor = '#3B82F6';
                        this.style.background = '#1a2a4a';
                        // Check the radio
                        var radio = this.querySelector('input[type="radio"]');
                        if (radio) radio.checked = true;
                    });
                });

                // Pre-select detected language
                if (detectResult.data && detectResult.data.detectedLanguage) {
                    selectedLanguage = detectResult.data.detectedLanguage;
                    var preOpt = langList.querySelector('[data-code="' + detectResult.data.detectedLanguage + '"]');
                    if (preOpt) {
                        preOpt.style.borderColor = '#3B82F6';
                        preOpt.style.background = '#1a2a4a';
                        var radio = preOpt.querySelector('input[type="radio"]');
                        if (radio) radio.checked = true;
                    }
                }
            }

            // Show detection info
            if (detectionDiv && detectResult.data) {
                if (detectResult.data.noFiles) {
                    detectionDiv.textContent = 'No language files found.';
                    detectionDiv.style.color = '#F59E0B';
                } else if (detectResult.data.fallbackUsed) {
                    detectionDiv.textContent = 'System language "' + detectResult.data.systemLanguage + '" not available. Falling back to English (US).';
                    detectionDiv.style.color = '#F59E0B';
                } else {
                    detectionDiv.textContent = 'Detected system language: ' + (detectResult.data.systemLanguage || 'unknown') + ' → ' + (detectResult.data.detectedLanguage || 'en-US');
                    detectionDiv.style.color = '#10B981';
                }
            }
        }).catch(function (err) {
            console.error('[SetupWizard] Failed to load languages:', err);
            var langList = document.getElementById('languageList');
            if (langList) langList.innerHTML = '<div style="color:#EF4444; font-size:13px; padding:12px;">Failed to load languages.</div>';
        });
    }

    // Navigation
    wizard.goNext = function () {
        if (currentStep < totalSteps - 1) {
            currentStep++;
            updateStepVisibility();
            updateProgressDots();
            updateButtons();
        }
    };

    wizard.goBack = function () {
        if (currentStep > 0) {
            currentStep--;
            updateStepVisibility();
            updateProgressDots();
            updateButtons();
        }
    };

    wizard.skip = function () {
        // Skip to finish step
        currentStep = totalSteps - 1;
        updateStepVisibility();
        updateProgressDots();
        updateButtons();
    };

    wizard.complete = function () {
        // Collect theme selection
        var themeRadio = document.querySelector('input[name="theme"]:checked');
        if (themeRadio) {
            selectedTheme = themeRadio.value;
        }

        // If no language selected, default to en-US
        if (!selectedLanguage) {
            selectedLanguage = 'en-US';
        }

        // Save settings via API
        var btn = document.getElementById('wizardFinishBtn') || document.getElementById('wizardNextBtn');
        if (btn) {
            btn.disabled = true;
            btn.textContent = 'Saving...';
        }

        fetch('/api/setup/complete', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                language: selectedLanguage,
                theme: selectedTheme
            })
        }).then(function (response) {
            return response.json();
        }).then(function (data) {
            if (data.success) {
                // Hide wizard and redirect to main page
                var overlay = document.getElementById('setupWizardOverlay');
                if (overlay) {
                    overlay.style.transition = 'opacity 0.3s';
                    overlay.style.opacity = '0';
                    setTimeout(function () {
                        window.location.href = '/';
                    }, 300);
                } else {
                    window.location.href = '/';
                }
            } else {
                console.error('[SetupWizard] Failed to save settings:', data.error);
                if (btn) {
                    btn.disabled = false;
                    btn.textContent = 'Start Using Visunovia';
                }
            }
        }).catch(function (err) {
            console.error('[SetupWizard] Error saving settings:', err);
            if (btn) {
                btn.disabled = false;
                btn.textContent = 'Start Using Visunovia';
            }
        });
    };

    function updateStepVisibility() {
        for (var i = 0; i < totalSteps; i++) {
            var stepEl = document.getElementById('wizardStep' + i);
            if (stepEl) {
                stepEl.style.display = (i === currentStep) ? 'block' : 'none';
            }
        }

        // Update title/subtitle
        var titleEl = document.getElementById('wizardTitle');
        var subtitleEl = document.getElementById('wizardSubtitle');

        if (currentStep === 0) {
            if (titleEl) titleEl.textContent = 'Choose Language';
            if (subtitleEl) subtitleEl.textContent = 'Select your preferred language for the application interface.';
        } else if (currentStep === 1) {
            if (titleEl) titleEl.textContent = 'Choose Theme';
            if (subtitleEl) subtitleEl.textContent = 'Select your preferred color theme.';
        } else if (currentStep === 2) {
            if (titleEl) titleEl.textContent = 'Ready to Go';
            if (subtitleEl) subtitleEl.textContent = 'Your preferences have been saved. You can change them anytime in Settings.';
        }
    }

    function updateProgressDots() {
        var dots = document.querySelectorAll('.wizard-step-dot');
        dots.forEach(function (dot, idx) {
            if (idx === currentStep) {
                dot.style.background = '#3B82F6';
                dot.style.transform = 'scale(1.2)';
            } else if (idx < currentStep) {
                dot.style.background = '#10B981';
                dot.style.transform = 'scale(1)';
            } else {
                dot.style.background = '#444';
                dot.style.transform = 'scale(1)';
            }
        });
    }

    function updateButtons() {
        var backBtn = document.getElementById('wizardBackBtn');
        var skipBtn = document.getElementById('wizardSkipBtn');
        var nextBtn = document.getElementById('wizardNextBtn');

        if (backBtn) {
            backBtn.style.display = currentStep > 0 ? 'inline-block' : 'none';
        }

        if (skipBtn) {
            skipBtn.style.display = currentStep < totalSteps - 1 ? 'inline-block' : 'none';
        }

        if (nextBtn) {
            if (currentStep === totalSteps - 1) {
                nextBtn.textContent = 'Start Using Visunovia';
                nextBtn.onclick = wizard.complete;
            } else {
                nextBtn.textContent = 'Next';
                nextBtn.onclick = wizard.goNext;
            }
        }
    }

    function escapeHtml(str) {
        if (!str) return '';
        var div = document.createElement('div');
        div.textContent = str;
        return div.innerHTML;
    }

    // Auto-initialize when DOM is ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', wizard.init);
    } else {
        wizard.init();
    }

})();
