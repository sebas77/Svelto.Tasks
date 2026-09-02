#if UNITY_EDITOR && TASKS_PROFILER_ENABLED
using System.Collections.Generic;
using Unity.Profiling.Editor;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine.UIElements;
using Toolbar = UnityEditor.UIElements.Toolbar;

namespace Svelto.Tasks.Profiler
{
    /// <summary>
    /// Unity Profiler module exposing Svelto.Tasks scopes emitted by <see cref="UnityTaskProfilerDriver"/>.
    /// The chart uses the aggregate frame counters; the details view renders a CPU-module-style call
    /// tree limited to the threads and subtrees containing Svelto.Tasks activity.
    /// </summary>
    [System.Serializable]
    [ProfilerModuleMetadata("Svelto.Tasks")]
    public sealed class SveltoTasksProfilerModule : ProfilerModule
    {
        static readonly ProfilerCounterDescriptor[] _chartCounters =
        {
            new ProfilerCounterDescriptor(UnityTaskProfilerDriver.TaskTimeCounterName,
                UnityTaskProfilerDriver.CategoryName),
            new ProfilerCounterDescriptor(UnityTaskProfilerDriver.TaskStepsCounterName,
                UnityTaskProfilerDriver.CategoryName)
        };

        public SveltoTasksProfilerModule() : base(_chartCounters,
            autoEnabledCategoryNames: new[] { UnityTaskProfilerDriver.CategoryName }) { }

        public override ProfilerModuleViewController CreateDetailsViewController()
        {
            return new SveltoTasksProfilerDetailsViewController(ProfilerWindow);
        }
    }

    sealed class SveltoTasksProfilerDetailsViewController : ProfilerModuleViewController
    {
        const int RefreshIntervalMs = 250;

        internal const string ObjectColumnName = "object";
        internal const string TotalColumnName = "total";
        internal const string SelfColumnName = "self";
        internal const string CallsColumnName = "calls";
        internal const string GcColumnName = "gc";

        readonly List<TaskTreeItem> _roots = new List<TaskTreeItem>();
        readonly List<string> _entryThreadNames = new List<string>();
        readonly List<double> _entryTotalsMs = new List<double>();

        DropdownField _runnerDropdown;
        TextField _filterField;
        Label _summary;
        MultiColumnTreeView _tree;

        public SveltoTasksProfilerDetailsViewController(UnityEditor.ProfilerWindow profilerWindow) :
            base(profilerWindow) { }

        protected override VisualElement CreateView()
        {
            var root = new VisualElement();
            root.style.flexGrow = 1;

            var toolbar = new Toolbar();
            //the tree below grows into all remaining space; without this the flex layout
            //squeezes the toolbar vertically until its text clips (seen as a thin strip)
            toolbar.style.flexShrink = 0;
            toolbar.style.height = 30;
            toolbar.style.paddingLeft = 8;
            toolbar.style.paddingRight = 8;

            _runnerDropdown = new DropdownField("Runner", new List<string>(), 0);
            _runnerDropdown.style.width = 540;
            _runnerDropdown.style.height = 24;
            _runnerDropdown.style.marginTop = 3;
            _runnerDropdown.style.marginRight = 8;
            _runnerDropdown.RegisterValueChangedCallback(_ => RebuildTree());
            toolbar.Add(_runnerDropdown);

            _filterField = new TextField("Filter") { tooltip =
                "case-insensitive substring on runner/task names; matches across all threads" };
            _filterField.style.width = 250;
            _filterField.style.height = 24;
            _filterField.style.marginTop = 3;
            _filterField.style.flexShrink = 0;
            _filterField.RegisterValueChangedCallback(_ => RebuildTree());
            toolbar.Add(_filterField);

            //keeps the summary clear of the controls above even when the window is narrow
            var spacer = new VisualElement();
            spacer.style.flexGrow = 1f;
            spacer.style.minWidth = 8;
            toolbar.Add(spacer);

            _summary = new Label(string.Empty);
            _summary.style.marginLeft = 8;
            _summary.style.height = 30;
            _summary.style.unityTextAlign = UnityEngine.TextAnchor.MiddleLeft;
            toolbar.Add(_summary);

            root.Add(toolbar);

            _tree = CreateTreeView();
            root.Add(_tree);

            root.schedule.Execute(Refresh).Every(RefreshIntervalMs);
            Refresh();

            return root;
        }

        MultiColumnTreeView CreateTreeView()
        {
            var tree = new MultiColumnTreeView
            {
                showBorder = true,
                selectionType = SelectionType.Single,
                autoExpand = true,
                virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight,
                style = { flexGrow = 1 }
            };

            tree.columns.Add(CreateTextColumn(tree, ObjectColumnName, "Object", 260, float.MaxValue,
                UnityEngine.TextAnchor.MiddleLeft));
            tree.columns.Add(CreateTextColumn(tree, TotalColumnName, "Total", 90, 120,
                UnityEngine.TextAnchor.MiddleRight));
            tree.columns.Add(CreateTextColumn(tree, SelfColumnName, "Self", 80, 110,
                UnityEngine.TextAnchor.MiddleRight));
            tree.columns.Add(CreateTextColumn(tree, CallsColumnName, "Calls", 60, 80,
                UnityEngine.TextAnchor.MiddleRight));
            tree.columns.Add(CreateTextColumn(tree, GcColumnName, "GC Alloc", 90, 120,
                UnityEngine.TextAnchor.MiddleRight));

            //Ctrl+C copies the selected row's full task name
            tree.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.ctrlKey == false || e.keyCode != UnityEngine.KeyCode.C)
                    return;

                foreach (var entry in tree.GetSelectedItems<object>())
                {
                    if (entry.data is TaskTreeItem item)
                        EditorGUIUtility.systemCopyBuffer = item.Name;
                    break;
                }

                e.StopPropagation();
            }, TrickleDown.TrickleDown);

            return tree;
        }

        Column CreateTextColumn(MultiColumnTreeView tree, string name, string title, float minWidth,
            float maxWidth, UnityEngine.TextAnchor alignment)
        {
            var column = new Column
            {
                name = name,
                title = title,
                minWidth = minWidth,
                maxWidth = maxWidth
            };

            column.makeCell = () =>
            {
                var label = new Label();
                label.style.unityTextAlign = alignment;
                label.style.marginLeft = 2;
                label.style.marginRight = 4;

                //right-click copies the cell text; labels hold the full untruncated string
                label.AddManipulator(new ContextualMenuManipulator(populateEvent =>
                {
                    var cellText = ((Label)populateEvent.target).text;
                    populateEvent.menu.AppendAction("Copy",
                        _ => EditorGUIUtility.systemCopyBuffer = cellText);
                }));

                return label;
            };

            column.bindCell = (element, rowIndex) =>
            {
                var item = tree.GetItemDataForIndex<object>(rowIndex) as TaskTreeItem;
                if (item == null)
                    return;

                var label = (Label)element;
                label.text = item.GetText(name);

                if (name == TotalColumnName)
                {
                    //hotspot shading: the dominant branch reads red-tinted at a glance
                    if (item.ShareOfVisible > 0.5f)
                        label.style.color = new UnityEngine.Color(1f, 0.45f, 0.35f);
                    else
                        label.style.color = new StyleColor(StyleKeyword.Null);
                }
            };

            return column;
        }

        void Refresh()
        {
            ScanRunners((int)ProfilerWindow.selectedFrameIndex);
            UpdateRunnerSelection();
            RebuildTree();
        }

        /// <summary>
        /// Lists one entry per runner scope that ran this frame. Runner scopes are identified by
        /// their "Runner/" marker prefix; each entry remembers the thread it executed on. Threads
        /// showing task activity without any runner scope fall back to a single thread entry.
        /// </summary>
        void ScanRunners(int frameIndex)
        {
            _entryThreadNames.Clear();
            _entryTotalsMs.Clear();
            _runnerDropdown.choices.Clear();

            if (frameIndex < 0)
                return;

            //counter values are streamed as pseudo samples of our category; they must not
            //make a thread look like it executed tasks
            var counters = new[]
            {
                UnityTaskProfilerDriver.TaskTimeCounterName,
                UnityTaskProfilerDriver.TaskStepsCounterName
            };

            for (var threadIndex = 0; ; threadIndex++)
            {
                using (var frameData = ProfilerDriver.GetRawFrameDataView(frameIndex, threadIndex))
                {
                    if (frameData.valid == false)
                        break;

                    double otherTotalMs = 0;
                    var runnerTotalsMs = new Dictionary<string, double>();

                    for (var sampleIndex = 0; sampleIndex < frameData.sampleCount; sampleIndex++)
                    {
                        var name = frameData.GetSampleName(sampleIndex);
                        if (name == counters[0] || name == counters[1])
                            continue;

                        var categoryIndex = frameData.GetSampleCategoryIndex(sampleIndex);
                        if (frameData.GetCategoryInfo(categoryIndex).name !=
                            UnityTaskProfilerDriver.CategoryName)
                            continue;

                        var sampleMs = frameData.GetSampleTimeMs(sampleIndex);

                        if (name.StartsWith("Runner/") == true)
                        {
                            var runnerKey = Svelto.Tasks.Profiler.TaskProfiler.NormalizeTaskName(name);
                            if (runnerTotalsMs.ContainsKey(runnerKey) == false)
                                runnerTotalsMs[runnerKey] = 0;
                            runnerTotalsMs[runnerKey] += sampleMs;
                        }
                        else
                            otherTotalMs += sampleMs;
                    }

                    foreach (var pair in runnerTotalsMs)
                        AddRunnerEntry(frameData.threadName, PrettyRunnerName(pair.Key), pair.Value);

                    if (runnerTotalsMs.Count == 0 && otherTotalMs > 0)
                        AddRunnerEntry(frameData.threadName, frameData.threadName, otherTotalMs);
                }
            }
        }

        void AddRunnerEntry(string threadName, string displayName, double totalMs)
        {
            _entryThreadNames.Add(threadName);
            _entryTotalsMs.Add(totalMs);
            _runnerDropdown.choices.Add(displayName);
        }

        internal static string PrettyRunnerName(string normalizedThreadName)
        {
            var name = normalizedThreadName.StartsWith("Runner/") == true
                ? normalizedThreadName.Substring("Runner/".Length)
                : normalizedThreadName;

            return name.EndsWith(" runner") == true
                ? name.Substring(0, name.Length - " runner".Length)
                : name;
        }

        void UpdateRunnerSelection()
        {
            if (_runnerDropdown.choices.Count == 0)
            {
                _runnerDropdown.SetEnabled(false);
                return;
            }

            _runnerDropdown.SetEnabled(true);

            var selectedIndex = _runnerDropdown.index;

            //no previous selection (or out of range after a scan): default to the busiest runner
            if (selectedIndex < 0 || selectedIndex >= _entryTotalsMs.Count)
            {
                selectedIndex = 0;
                for (var i = 1; i < _entryTotalsMs.Count; i++)
                    if (_entryTotalsMs[i] > _entryTotalsMs[selectedIndex])
                        selectedIndex = i;

                _runnerDropdown.index = selectedIndex;
                _runnerDropdown.SetValueWithoutNotify(_runnerDropdown.choices[selectedIndex]);
            }
        }

        void RebuildTree()
        {
            _roots.Clear();

            var frameIndex = (int)ProfilerWindow.selectedFrameIndex;
            var filter = _filterField == null ? string.Empty : _filterField.text.Trim();

            if (frameIndex < 0)
            {
                FinishRebuild(frameIndex);
                return;
            }

            //a filter spans every active thread; otherwise the selected runner's thread is shown
            if (filter.Length > 0)
            {
                _runnerDropdown.SetEnabled(false);

                for (var threadIndex = 0; ; threadIndex++)
                {
                    using (var hierarchy = ProfilerDriver.GetHierarchyFrameDataView(frameIndex,
                               threadIndex, HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName,
                               HierarchyFrameDataView.columnTotalTime, false))
                    {
                        if (hierarchy.valid == false)
                            break;

                        CollectMatchingScopes(hierarchy, filter);
                    }
                }

                FinishRebuild(frameIndex);
                return;
            }

            _runnerDropdown.SetEnabled(true);

            //the selected entry remembers which thread it executed on; the hierarchy view is
            //located by thread name because hierarchy indices are not guaranteed to match raw ones
            var selectedEntry = _runnerDropdown.index;
            if (selectedEntry >= 0 && selectedEntry < _entryThreadNames.Count)
            {
                var expectedThreadName = _entryThreadNames[selectedEntry];

                for (var threadIndex = 0; ; threadIndex++)
                {
                    using (var hierarchy = ProfilerDriver.GetHierarchyFrameDataView(frameIndex,
                               threadIndex, HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName,
                               HierarchyFrameDataView.columnTotalTime, false))
                    {
                        if (hierarchy.valid == false)
                            break;

                        if (hierarchy.threadName != expectedThreadName)
                            continue;

                        var topLevelIds = new List<int>();
                        hierarchy.GetItemChildren(hierarchy.GetRootItemID(), topLevelIds);
                        BuildPrunedSubtrees(hierarchy, topLevelIds);
                        break;
                    }
                }
            }

            FinishRebuild(frameIndex);
        }

        /// <summary>
        /// Filter mode: keeps only top-level Svelto scopes whose normalized name contains the
        /// filter, from any thread, with their descendants.
        /// </summary>
        void CollectMatchingScopes(HierarchyFrameDataView hierarchy, string filter)
        {
            var topLevelIds = new List<int>();
            hierarchy.GetItemChildren(hierarchy.GetRootItemID(), topLevelIds);

            foreach (var itemId in topLevelIds)
            {
                if (IsSveltoScopeItem(hierarchy, itemId) == false)
                    continue;

                var name = Svelto.Tasks.Profiler.TaskProfiler.NormalizeTaskName(
                    hierarchy.GetItemName(itemId));

                if (name.IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                var item = BuildItemRecursive(hierarchy, itemId);
                if (item != null)
                    _roots.Add(item);
            }
        }

        void FinishRebuild(int frameIndex)
        {
            _roots.Sort((a, b) => b.TotalMs.CompareTo(a.TotalMs));

            _summary.text = Summarize(frameIndex);

            _tree.SetRootItems(BuildViewItemData(_roots));
            if (_roots.Count > 0)
                _tree.ExpandAll();
        }

        /// <summary>
        /// Keeps only Svelto.Tasks scope subtrees; branches without Svelto content are dropped so
        /// unrelated profiler noise never reaches the view.
        /// </summary>
        void BuildPrunedSubtrees(HierarchyFrameDataView hierarchy, List<int> topLevelIds)
        {
            foreach (var itemId in topLevelIds)
            {
                var item = BuildItemRecursive(hierarchy, itemId);
                if (item != null)
                    _roots.Add(item);
            }
        }

        TaskTreeItem BuildItemRecursive(HierarchyFrameDataView hierarchy, int itemId)
        {
            var isSveltoScope = IsSveltoScopeItem(hierarchy, itemId);

            //per-call buffer: recursion must not mutate the list being iterated by callers
            var childIds = new List<int>();
            hierarchy.GetItemChildren(itemId, childIds);

            List<TaskTreeItem> children = null;
            foreach (var childId in childIds)
            {
                var child = BuildItemRecursive(hierarchy, childId);
                if (child == null)
                    continue;

                if (children == null)
                    children = new List<TaskTreeItem>();
                children.Add(child);
            }

            if (isSveltoScope == false && children == null)
                return null;

            if (children != null && children.Count > 1)
                children.Sort((a, b) => b.TotalMs.CompareTo(a.TotalMs));

            return new TaskTreeItem(hierarchy, itemId, children);
        }

        bool IsSveltoScopeItem(HierarchyFrameDataView hierarchy, int itemId)
        {
            var name = hierarchy.GetItemName(itemId);
            if (name == UnityTaskProfilerDriver.TaskTimeCounterName ||
                name == UnityTaskProfilerDriver.TaskStepsCounterName)
                return false;

            var categoryIndex = hierarchy.GetItemCategoryIndex(itemId);
            return hierarchy.GetCategoryInfo(categoryIndex).name == UnityTaskProfilerDriver.CategoryName;
        }

        string Summarize(int frameIndex)
        {
            if (frameIndex < 0)
                return "no frame selected";

            if (_roots.Count == 0)
                return $"frame {frameIndex}: no Svelto.Tasks activity";

            double totalMs = 0;
            uint calls = 0;
            foreach (var root in _roots)
            {
                totalMs += root.TotalMs;
                calls += root.Calls;
            }

            return $"frame {frameIndex}: {_roots.Count} runners   {calls} calls   {totalMs:F2} ms";
        }

        List<TreeViewItemData<object>> BuildViewItemData(List<TaskTreeItem> items)
        {
            double visibleTotalMs = 0;
            foreach (var root in items)
                visibleTotalMs += root.TotalMs;

            foreach (var root in items)
                root.ComputeShares(visibleTotalMs);

            var result = new List<TreeViewItemData<object>>(items.Count);
            AppendViewItemData(items, result);
            return result;
        }

        static void AppendViewItemData(List<TaskTreeItem> items,
            List<TreeViewItemData<object>> target)
        {
            foreach (var item in items)
            {
                var childData = new List<TreeViewItemData<object>>(item.Children.Count);
                AppendViewItemData(item.Children, childData);
                target.Add(new TreeViewItemData<object>(item.Id, item, childData));
            }
        }

        sealed class TaskTreeItem
        {
            internal TaskTreeItem(HierarchyFrameDataView hierarchy, int itemId,
                List<TaskTreeItem> children)
            {
                Id = itemId;

                //normalize at render time too: marker names are frozen into recorded frames,
                //so older captures still contain raw compiler-generated names
                Name = Svelto.Tasks.Profiler.TaskProfiler.NormalizeTaskName(
                    hierarchy.GetItemName(itemId));
                TotalMs =
                    hierarchy.GetItemColumnDataAsFloat(itemId, HierarchyFrameDataView.columnTotalTime);
                SelfMs =
                    hierarchy.GetItemColumnDataAsFloat(itemId, HierarchyFrameDataView.columnSelfTime);
                Calls = (uint)hierarchy.GetItemColumnDataAsFloat(itemId, HierarchyFrameDataView.columnCalls);
                GcAlloc = hierarchy.GetItemColumnData(itemId, HierarchyFrameDataView.columnGcMemory);
                Children = children ?? new List<TaskTreeItem>();
            }

            internal string GetText(string columnName)
            {
                switch (columnName)
                {
                    case ObjectColumnName: return Name;
                    case TotalColumnName: return $"{TotalMs:F3} ms ({ShareOfVisible * 100f:F0}%)";
                    case SelfColumnName: return $"{SelfMs:F3} ms";
                    case CallsColumnName: return Calls.ToString();
                    case GcColumnName: return GcAlloc;
                    default: return string.Empty;
                }
            }

            internal float ShareOfVisible { get; private set; }

            internal void ComputeShares(double visibleTotalMs)
            {
                ShareOfVisible = visibleTotalMs <= 0 ? 0 : (float)(TotalMs / visibleTotalMs);
                foreach (var child in Children)
                    child.ComputeShares(visibleTotalMs);
            }

            internal int Id { get; }
            internal string Name { get; }
            internal float TotalMs { get; }
            internal float SelfMs { get; }
            internal uint Calls { get; }
            internal string GcAlloc { get; }
            internal List<TaskTreeItem> Children { get; }
        }
    }
}
#endif
