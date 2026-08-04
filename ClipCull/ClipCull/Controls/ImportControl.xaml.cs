using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ClipCull.Core;
using ClipCull.Core.Import;
using ClipCull.Models;
using ClipCull.Models.Import;

namespace ClipCull.Controls
{
    /// <summary>
    /// The Import tab: scan a source (folder/card), pick media, organize into a target library,
    /// seed sidecars (video), and move/copy with duplicate detection.
    /// </summary>
    public partial class ImportControl : UserControl
    {
        private readonly ObservableCollection<ImportFileItem> _files = new();
        private ImportMediaKind _mediaKind = ImportMediaKind.Video;
        private string _sourcePath;
        private string _targetPath;
        private TargetIndex _targetIndex;
        private bool _reorganizeInPlace;

        private CancellationTokenSource _scanCts;
        private CancellationTokenSource _probeCts;
        private CancellationTokenSource _classifyCts;
        private bool _initialized;
        private bool _busy;

        // Tags applied to every imported clip. Available tags come from the active workspace.
        private readonly ObservableCollection<Tag> _importTags = new();
        private ObservableCollection<Tag> _availableTags;

        public ImportControl()
        {
            InitializeComponent();

            FilesListView.ItemsSource = _files;
            _files.CollectionChanged += Files_CollectionChanged;

            foreach (var preset in ImportOrganizer.DateFormatPresets)
                DateFormatCombo.Items.Add(preset);

            // Defaults from settings.
            var s = SettingsHandler.Settings;
            StructureCombo.SelectedIndex = (int)(s?.ImportStructureMode ?? ImportStructureMode.SubfolderPerDay);
            DateBasisCombo.SelectedIndex = (int)(s?.ImportDateBasis ?? ImportDateBasis.Created);
            DateFormatCombo.Text = string.IsNullOrWhiteSpace(s?.ImportDateFormat) ? ImportOrganizer.DefaultDateFormat : s.ImportDateFormat;

            _initialized = true;
            UpdateModeUi();
            UpdateDateFormatUi();
            UpdateCounts();
            InitTags();
        }

        #region Tags

        private void InitTags()
        {
            RefreshAvailableTags();

            TagControl.CurrentTags = _importTags;
            TagControl.AvailableTags = _availableTags;
            TagControl.AllowModifyAvailableTags = true;
            TagControl.IsReadOnly = false;

            // Keep the tag pool in sync with the active workspace.
            SettingsHandler.WorkspaceChanged += OnWorkspaceChanged;
        }

        private void RefreshAvailableTags()
        {
            if (_availableTags != null)
                _availableTags.CollectionChanged -= AvailableTags_CollectionChanged;

            _availableTags = new ObservableCollection<Tag>();
            foreach (var tag in SettingsHandler.GetCurrentWorkspaceTags())
                _availableTags.Add(tag);

            _availableTags.CollectionChanged += AvailableTags_CollectionChanged;
        }

        private void AvailableTags_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (_availableTags == null || _availableTags.Count == 0)
                return;

            // Newly created tags belong to the active workspace.
            SettingsHandler.CurrentWorkspace.Tags = _availableTags.ToList();
            SettingsHandler.Save();
        }

        private void OnWorkspaceChanged()
        {
            Dispatcher.Invoke(() =>
            {
                RefreshAvailableTags();
                TagControl.AvailableTags = _availableTags;
            });
        }

        #endregion

        #region Mode toggle

        private void VideoModeToggle_Click(object sender, RoutedEventArgs e) => SetMode(ImportMediaKind.Video);
        private void PhotoModeToggle_Click(object sender, RoutedEventArgs e) => SetMode(ImportMediaKind.Photo);

        private void SetMode(ImportMediaKind kind)
        {
            _mediaKind = kind;
            UpdateModeUi();
            if (!string.IsNullOrEmpty(_sourcePath))
                StartScan();
        }

        private void UpdateModeUi()
        {
            VideoModeToggle.IsChecked = _mediaKind == ImportMediaKind.Video;
            PhotoModeToggle.IsChecked = _mediaKind == ImportMediaKind.Photo;
            MetadataPanel.Visibility = _mediaKind == ImportMediaKind.Video ? Visibility.Visible : Visibility.Collapsed;
        }

        #endregion

        #region Source / target selection

        private void BrowseSource_Click(object sender, RoutedEventArgs e)
        {
            var folder = DialogHelper.ChooseFolder("Select the source folder or drive", _sourcePath ?? SettingsHandler.Settings?.LastImportSourcePath);
            if (string.IsNullOrEmpty(folder)) return;

            _sourcePath = folder;
            SourceTextBox.Text = folder;

            // In reorganize mode the target always mirrors the source.
            if (_reorganizeInPlace)
            {
                _targetPath = folder;
                TargetTextBox.Text = folder;
            }

            StartScan();
        }

        private void BrowseTarget_Click(object sender, RoutedEventArgs e)
        {
            var folder = DialogHelper.ChooseFolder("Select the target library folder", _targetPath ?? SettingsHandler.Settings?.LastImportTargetPath);
            if (string.IsNullOrEmpty(folder)) return;

            _targetPath = folder;
            TargetTextBox.Text = folder;
            _ = BuildTargetIndexAndClassify();
        }

        private void ReorganizeCheck_Changed(object sender, RoutedEventArgs e)
        {
            _reorganizeInPlace = ReorganizeCheck.IsChecked == true;

            // When reorganizing, the target is the source folder itself.
            TargetBrowseButton.IsEnabled = !_reorganizeInPlace;
            TargetLabel.Text = _reorganizeInPlace ? "Target (same as source)" : "Target (library folder)";

            if (_reorganizeInPlace)
            {
                _targetPath = _sourcePath;
                TargetTextBox.Text = _sourcePath ?? string.Empty;
                if (!string.IsNullOrEmpty(_targetPath))
                    _ = BuildTargetIndexAndClassify();
            }
        }

        #endregion

        #region Scan

        private async void StartScan()
        {
            _scanCts?.Cancel();
            _probeCts?.Cancel();
            var cts = _scanCts = new CancellationTokenSource();

            _files.Clear();
            _targetIndex = null; // conflicts are relative to a target; recompute after rescan
            EmptyLabel.Text = "Scanning…";
            EmptyPanel.Visibility = Visibility.Visible;
            SetStatus($"Scanning {_sourcePath}…");

            PrefillFromStorageInfo();

            try
            {
                var progress = new Progress<int>(count => SetStatus($"Scanning… found {count} file(s)"));
                var items = await ImportScanner.ScanAsync(_sourcePath, _mediaKind, progress, cts.Token);
                if (cts.IsCancellationRequested) return;

                foreach (var item in items)
                {
                    item.PropertyChanged += Item_PropertyChanged;
                    _files.Add(item);
                }

                EmptyLabel.Text = _files.Count == 0
                    ? $"No {_mediaKind.ToString().ToLower()} files found in the source"
                    : string.Empty;
                SetStatus($"Found {_files.Count} {_mediaKind.ToString().ToLower()} file(s)");
                UpdateCounts();

                StartProbing(items);

                if (!string.IsNullOrEmpty(_targetPath))
                    await BuildTargetIndexAndClassify();
            }
            catch (OperationCanceledException) { /* superseded */ }
            catch (Exception ex)
            {
                Logger.LogError("Failed to scan source", ex);
                SetStatus("Scan failed");
            }
        }

        private void StartProbing(System.Collections.Generic.IReadOnlyList<ImportFileItem> items)
        {
            _probeCts?.Cancel();
            var cts = _probeCts = new CancellationTokenSource();
            _ = ProbeAllAsync(items, cts.Token);
        }

        private static async Task ProbeAllAsync(System.Collections.Generic.IReadOnlyList<ImportFileItem> items, CancellationToken token)
        {
            using var sem = new SemaphoreSlim(3);
            try
            {
                var tasks = items.Select(async item =>
                {
                    await sem.WaitAsync(token);
                    try { await Task.Run(() => ImportProbeService.Probe(item, token), token); }
                    finally { sem.Release(); }
                });
                await Task.WhenAll(tasks);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { Logger.LogDebug($"Probing stopped: {ex.Message}"); }
        }

        private void PrefillFromStorageInfo()
        {
            if (_mediaKind != ImportMediaKind.Video) return;
            var info = StorageInfoService.Read(_sourcePath);
            if (info == null) return;

            if (!string.IsNullOrWhiteSpace(info.CameraModel) && string.IsNullOrWhiteSpace(MetaCamera.Text)) MetaCamera.Text = info.CameraModel;
            if (!string.IsNullOrWhiteSpace(info.Reel) && string.IsNullOrWhiteSpace(MetaReel.Text)) MetaReel.Text = info.Reel;
            if (!string.IsNullOrWhiteSpace(info.Author) && string.IsNullOrWhiteSpace(MetaAuthor.Text)) MetaAuthor.Text = info.Author;
            if (!string.IsNullOrWhiteSpace(info.Location) && string.IsNullOrWhiteSpace(MetaLocation.Text)) MetaLocation.Text = info.Location;
        }

        #endregion

        #region Conflict detection

        private async Task BuildTargetIndexAndClassify()
        {
            if (string.IsNullOrEmpty(_targetPath)) return;

            _classifyCts?.Cancel();
            var cts = _classifyCts = new CancellationTokenSource();

            try
            {
                SetStatus("Indexing target folder…");
                _targetIndex = await TargetIndex.BuildAsync(_targetPath, cts.Token);
                await ClassifyAsync(cts.Token);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Logger.LogError("Failed to index target folder", ex);
            }
        }

        private async Task ClassifyAsync(CancellationToken token)
        {
            if (_targetIndex == null) return;

            var mode = CurrentStructureMode();
            var basis = CurrentDateBasis();
            var format = DateFormatCombo.Text;
            var snapshot = _files.ToList();

            // Destinations must be set first (classification compares against them).
            foreach (var item in snapshot)
                ImportOrganizer.SetNaturalDestination(item, _targetPath, mode, basis, format);

            SetStatus("Checking for duplicates…");
            await Task.Run(() =>
            {
                foreach (var item in snapshot)
                {
                    token.ThrowIfCancellationRequested();
                    _targetIndex.Classify(item);
                }
            }, token);

            int conflicts = snapshot.Count(i => i.HasConflict);
            SetStatus(conflicts > 0 ? $"{conflicts} possible duplicate(s) found" : "No duplicates found");
        }

        #endregion

        #region Options

        private void Options_Changed(object sender, RoutedEventArgs e)
        {
            if (!_initialized) return;
            UpdateDateFormatUi();
            // Re-evaluate destinations + conflicts when the structure changes.
            if (_targetIndex != null)
                _ = ClassifyAsync((_classifyCts ??= new CancellationTokenSource()).Token);
        }

        private void UpdateDateFormatUi()
        {
            bool perDay = CurrentStructureMode() == ImportStructureMode.SubfolderPerDay;
            DateFormatLabel.Visibility = perDay ? Visibility.Visible : Visibility.Collapsed;
            DateFormatCombo.Visibility = perDay ? Visibility.Visible : Visibility.Collapsed;
            DateFormatPreview.Visibility = perDay ? Visibility.Visible : Visibility.Collapsed;

            if (perDay)
            {
                try { DateFormatPreview.Text = "e.g. " + DateTime.Now.ToString(DateFormatCombo.Text, CultureInfo.InvariantCulture); }
                catch { DateFormatPreview.Text = "Invalid format"; }
            }
        }

        private ImportStructureMode CurrentStructureMode() =>
            (ImportStructureMode)Math.Max(0, StructureCombo.SelectedIndex);

        private ImportDateBasis CurrentDateBasis() =>
            (ImportDateBasis)Math.Max(0, DateBasisCombo.SelectedIndex);

        #endregion

        #region Selection tools

        private void SelectAll_Click(object sender, RoutedEventArgs e) => SetAll(true);
        private void DeselectAll_Click(object sender, RoutedEventArgs e) => SetAll(false);

        private void SetAll(bool selected)
        {
            foreach (var item in _files) item.IsSelected = selected;
            UpdateCounts();
        }

        private void SelectRange_Click(object sender, RoutedEventArgs e) => ApplyRange(true);
        private void DeselectRange_Click(object sender, RoutedEventArgs e) => ApplyRange(false);

        private void ApplyRange(bool selected)
        {
            DateTime? from = RangeFromPicker.SelectedDate;
            DateTime? to = RangeToPicker.SelectedDate;
            if (from == null && to == null)
            {
                MessageBox.Show("Pick a From and/or To date first.", "Select range",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var basis = CurrentDateBasis();
            DateTime lower = from?.Date ?? DateTime.MinValue;
            DateTime upper = to?.Date.AddDays(1).AddTicks(-1) ?? DateTime.MaxValue;

            foreach (var item in _files)
            {
                var d = item.GetDate(basis);
                if (d >= lower && d <= upper)
                    item.IsSelected = selected;
            }
            UpdateCounts();
        }

        #endregion

        #region Storage info

        private void EditStorageInfo_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new StorageInfoEditDialog(_sourcePath)
            {
                Owner = Window.GetWindow(this)
            };
            if (dialog.ShowDialog() == true)
                PrefillFromStorageInfo();
        }

        #endregion

        #region Import

        private async void Import_Click(object sender, RoutedEventArgs e)
        {
            if (_busy) return;

            if (string.IsNullOrEmpty(_sourcePath) || !Directory.Exists(_sourcePath))
            {
                MessageBox.Show("Choose a valid source folder first.", "Import", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (string.IsNullOrEmpty(_targetPath))
            {
                MessageBox.Show("Choose a target folder first.", "Import", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_reorganizeInPlace && CurrentStructureMode() == ImportStructureMode.SameFolder)
            {
                MessageBox.Show("Pick a per-day, per-week or per-month structure to reorganize into.",
                    "Reorganize", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!_files.Any(f => f.IsSelected))
            {
                MessageBox.Show("No files are selected for import.", "Import", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Refresh duplicate detection against the target's CURRENT contents so the confirm
            // dialog and the per-row badges reflect reality – e.g. an earlier partial import may
            // already have copied some of these files. This also (re)computes destinations.
            ImportButton.IsEnabled = false;
            SetStatus("Checking target for duplicates…");
            try { await BuildTargetIndexAndClassify(); }
            finally { ImportButton.IsEnabled = true; }

            var selected = _files.Where(f => f.IsSelected).ToList();
            int skipCount = selected.Count(i => i.ConflictResolutionChoice == ConflictResolution.Skip);
            int importCount = selected.Count - skipCount;
            if (importCount == 0)
            {
                MessageBox.Show(
                    "Every selected file already exists in the target folder – nothing to import.",
                    "Import", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var s = SettingsHandler.Settings;
            var confirm = new ImportConfirmDialog(importCount, skipCount, _mediaKind,
                s?.ImportOperation ?? ImportOperation.Copy,
                s?.ImportOpenExplorerAfter ?? true,
                s?.ImportOpenEditorAfter ?? false,
                reorganize: _reorganizeInPlace)
            {
                Owner = Window.GetWindow(this)
            };
            if (confirm.ShowDialog() != true)
                return;

            // Reorganize is always a move; don't let it overwrite the remembered import operation.
            var operationToPersist = _reorganizeInPlace
                ? (s?.ImportOperation ?? ImportOperation.Copy)
                : confirm.Operation;
            PersistSettings(operationToPersist, confirm.OpenExplorerAfter, confirm.OpenEditorAfter);

            var template = _mediaKind == ImportMediaKind.Video ? BuildMetadataTemplate() : null;

            _busy = true;
            ImportButton.IsEnabled = false;
            ProgressBarControl.Visibility = Visibility.Visible;
            ProgressBarControl.IsIndeterminate = false;
            ProgressBarControl.Value = 0;

            try
            {
                var progress = new Progress<ImportProgress>(p =>
                {
                    ProgressBarControl.Maximum = p.Total;
                    ProgressBarControl.Value = p.Processed;
                    SetStatus($"Importing {p.Processed}/{p.Total}: {p.CurrentFile}");
                });

                var result = await ImportExecutor.RunAsync(selected, confirm.Operation, template, progress);

                SetStatus($"Imported {result.Imported}, skipped {result.Skipped}" +
                          (result.Failed > 0 ? $", failed {result.Failed}" : ""));

                if (result.Failed > 0)
                {
                    Logger.LogWarning($"Import finished with {result.Failed} error(s).", "Import");

                    var preview = string.Join(Environment.NewLine, result.Errors.Take(15));
                    if (result.Errors.Count > 15)
                        preview += $"{Environment.NewLine}… and {result.Errors.Count - 15} more (see log).";

                    MessageBox.Show(
                        $"Imported {result.Imported}, skipped {result.Skipped}, failed {result.Failed}.\n\n{preview}",
                        "Import errors", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                else
                {
                    var msg = $"Imported {result.Imported} file(s)";
                    if (result.Skipped > 0) msg += $", skipped {result.Skipped} already present";
                    Logger.LogSuccess(msg + $" → {_targetPath}", "Import");
                }

                DoPostImportActions(confirm.OpenExplorerAfter, confirm.OpenEditorAfter);

                // Keep the grid honest afterwards: a move removes the source files, a copy makes
                // the just-imported files show up as already-present duplicates on a re-run.
                if (confirm.Operation == ImportOperation.Move)
                    StartScan();
                else
                    _ = BuildTargetIndexAndClassify();
            }
            catch (Exception ex)
            {
                Logger.LogError("Import failed", ex);
                SetStatus("Import failed");
            }
            finally
            {
                _busy = false;
                ImportButton.IsEnabled = true;
                ProgressBarControl.Visibility = Visibility.Collapsed;
            }
        }

        private UserMetadataContent BuildMetadataTemplate()
        {
            return new UserMetadataContent
            {
                Author = NullIfEmpty(MetaAuthor.Text),
                Location = NullIfEmpty(MetaLocation.Text),
                Reel = NullIfEmpty(MetaReel.Text),
                Shot = NullIfEmpty(MetaShot.Text),
                Camera = NullIfEmpty(MetaCamera.Text),
                Description = NullIfEmpty(MetaDescription.Text),
                Tags = new ObservableCollection<Tag>(_importTags)
                // Rating intentionally left unset.
            };
        }

        private void DoPostImportActions(bool openExplorer, bool openEditor)
        {
            try
            {
                if (openExplorer && Directory.Exists(_targetPath))
                    Process.Start("explorer.exe", $"\"{_targetPath}\"");
            }
            catch (Exception ex) { Logger.LogDebug($"Open explorer failed: {ex.Message}"); }

            if (openEditor && _mediaKind == ImportMediaKind.Video)
            {
                try { Globals.mainWindow?.OpenFolderInEditor(_targetPath); }
                catch (Exception ex) { Logger.LogDebug($"Open in editor failed: {ex.Message}"); }
            }
        }

        private void PersistSettings(ImportOperation operation, bool openExplorer, bool openEditor)
        {
            try
            {
                var s = SettingsHandler.Settings;
                if (s == null) return;
                s.LastImportSourcePath = _sourcePath;
                s.LastImportTargetPath = _targetPath;
                s.ImportStructureMode = CurrentStructureMode();
                s.ImportDateBasis = CurrentDateBasis();
                s.ImportDateFormat = DateFormatCombo.Text;
                s.ImportOperation = operation;
                s.ImportOpenExplorerAfter = openExplorer;
                s.ImportOpenEditorAfter = openEditor;
                SettingsHandler.Save();
            }
            catch (Exception ex) { Logger.LogDebug($"Could not persist import settings: {ex.Message}"); }
        }

        #endregion

        #region Helpers

        private void Files_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e) => UpdateCounts();

        private void Item_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ImportFileItem.IsSelected))
                Dispatcher.BeginInvoke(new Action(UpdateCounts));
        }

        private void UpdateCounts()
        {
            int total = _files.Count;
            int selected = _files.Count(f => f.IsSelected);
            SelectionSummary.Text = $"{selected} of {total} selected";
            EmptyPanel.Visibility = total == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SetStatus(string text)
        {
            if (Dispatcher.CheckAccess())
                StatusLabel.Text = text;
            else
                Dispatcher.BeginInvoke(new Action(() => StatusLabel.Text = text));
        }

        private static string NullIfEmpty(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

        #endregion
    }
}
