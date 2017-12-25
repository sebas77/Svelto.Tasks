#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

//This profiler is based on the Entitas Visual Debugging tool 
//https://github.com/sschmid/Entitas-CSharp

namespace Svelto.Tasks.Profiler
{
    [CustomEditor(typeof (TasksProfilerBehaviour))]
    public class TasksProfilerInspector : Editor
    {
        enum SORTING_OPTIONS
        {
            AVERAGE,
            MIN,
            MAX,
            NAME,
            NONE
        }

        static bool _showTickTasks;

        static string _systemNameSearchTerm = string.Empty;

        float _axisUpperBounds = 2f;

        string updateTitle = "Update".PadRight(15, ' ');
        string minTitle = "Min".PadRight(15, ' ');
        string maxTitle = "Max";
        
        TasksMonitor _tasksMonitor;
        Queue<float> _taskMonitorData;
        SORTING_OPTIONS _sortingOption = SORTING_OPTIONS.AVERAGE;

        const int SYSTEM_MONITOR_DATA_LENGTH = 300;

        TaskInfo[] tasks = new TaskInfo[1];
        
        public override void OnInspectorGUI()
        {
            var taskProfilerBehaviour = (TasksProfilerBehaviour) target;
            var tasksV = taskProfilerBehaviour.tasks;

            if (tasks.Length != tasksV.Count)
                tasks = new TaskInfo[tasksV.Count];

            taskProfilerBehaviour.tasks.CopyTo(tasks, 0);

            DrawTasksMonitor(tasks);
            DrawTaskList(taskProfilerBehaviour, tasks);

            EditorUtility.SetDirty(target);
        }

        void DrawTaskList(TasksProfilerBehaviour taskProfilerBehaviour, TaskInfo[] tasks)
        {
            ProfilerEditorLayout.BeginVerticalBox();
            {
                ProfilerEditorLayout.BeginHorizontal();
                {
                    if (GUILayout.Button("Remove Finished Tasks", GUILayout.Width(180), GUILayout.Height(14)))
                    {
                        taskProfilerBehaviour.ResetDurations();
                    }
                }
                ProfilerEditorLayout.EndHorizontal();

                _sortingOption = (SORTING_OPTIONS) EditorGUILayout.EnumPopup("Sort By:", _sortingOption);

                EditorGUILayout.Space();

                ProfilerEditorLayout.BeginHorizontal();
                {
                    _systemNameSearchTerm = EditorGUILayout.TextField("Search", _systemNameSearchTerm);

                    const string clearButtonControlName = "Clear Button";
                    GUI.SetNextControlName(clearButtonControlName);
                    if (GUILayout.Button("x", GUILayout.Width(19), GUILayout.Height(14)))
                    {
                        _systemNameSearchTerm = string.Empty;
                        GUI.FocusControl(clearButtonControlName);
                    }
                }
                ProfilerEditorLayout.EndHorizontal();

                _showTickTasks = EditorGUILayout.Foldout(_showTickTasks, "Tasks Ticks");
         //       if (_showTickTasks && ShouldShowSystems(tasks))
                {
                    ProfilerEditorLayout.BeginVerticalBox();
                    {
                        var systemsDrawn = DrawUpdateTaskInfos(tasks);
                        if (systemsDrawn == 0)
                        {
                            EditorGUILayout.LabelField(string.Empty);
                        }
                    }
                    ProfilerEditorLayout.EndVertical();
                }
            }
            ProfilerEditorLayout.EndVertical();
        }

        void DrawTasksMonitor(TaskInfo[] tasks)
        {
            if (_tasksMonitor == null)
            {
                _tasksMonitor = new TasksMonitor(SYSTEM_MONITOR_DATA_LENGTH);
                _taskMonitorData = new Queue<float>(new float[SYSTEM_MONITOR_DATA_LENGTH]);
                if (EditorApplication.update != Repaint)
                {
                    EditorApplication.update += Repaint;
                }
            }
            double totalDuration = 0;
            for (int i = 0; i < tasks.Length; i++)
            {
                totalDuration += tasks[i].lastUpdateDuration;
            }

            ProfilerEditorLayout.BeginVerticalBox();
            {
                EditorGUILayout.LabelField("Execution duration", EditorStyles.boldLabel);

                ProfilerEditorLayout.BeginHorizontal();
                {
                    EditorGUILayout.LabelField("Total", totalDuration.ToString());
                }
                ProfilerEditorLayout.EndHorizontal();

                ProfilerEditorLayout.BeginHorizontal();
                {
                    _axisUpperBounds = EditorGUILayout.FloatField("Axis Upper Bounds", _axisUpperBounds);
                }
                ProfilerEditorLayout.EndHorizontal();

                if (!EditorApplication.isPaused)
                {
                    if (_taskMonitorData.Count >= SYSTEM_MONITOR_DATA_LENGTH)
                    {
                        _taskMonitorData.Dequeue();
                    }

                    _taskMonitorData.Enqueue((float) totalDuration);
                }
                _tasksMonitor.Draw(_taskMonitorData.ToArray(), 80f, _axisUpperBounds);
            }
            ProfilerEditorLayout.EndVertical();
        }

        int DrawUpdateTaskInfos(TaskInfo[] tasks)
        {
            if (_sortingOption != SORTING_OPTIONS.NONE)
            {
                SortUpdateTasks(tasks);
            }

            string title =
                updateTitle
                    .FastConcat(minTitle)
                    .FastConcat(maxTitle);

            ProfilerEditorLayout.BeginHorizontal();
            {
                EditorGUILayout.LabelField("Task Name", EditorStyles.boldLabel);
                EditorGUILayout.TextArea(title, EditorStyles.boldLabel, GUILayout.MaxWidth(200));
            }
            ProfilerEditorLayout.EndHorizontal();

            int tasksDrawn = 0;
            for (int i = 0; i < tasks.Length; i++)
            {
                TaskInfo taskInfo = tasks[i];

                if (taskInfo.taskName.ToLower().Contains(_systemNameSearchTerm.ToLower()))
                {
                    ProfilerEditorLayout.BeginHorizontal();
                    {
                        var avg = string.Format("{0:0.000}", taskInfo.averageUpdateDuration).PadRight(15);
                        var min = string.Format("{0:0.000}", taskInfo.minUpdateDuration).PadRight(15);
                        var max = string.Format("{0:0.000}", taskInfo.maxUpdateDuration);

                        string output = avg.FastConcat(min).FastConcat(max);

                        EditorGUILayout.LabelField(taskInfo.taskName);
                        EditorGUILayout.TextArea(output, GetTaskStyle(), GUILayout.MaxWidth(200));
                    }
                    ProfilerEditorLayout.EndHorizontal();

                    tasksDrawn += 1;
                }
            }
            return tasksDrawn;
        }


        static GUIStyle GetTaskStyle()
        {
            var style = new GUIStyle(GUI.skin.label);
            var color = EditorGUIUtility.isProSkin ? Color.white : style.normal.textColor;

            style.normal.textColor = color;

            return style;
        }

        static bool ShouldShowSystems(TaskInfo[] tasks)
        {
            return tasks.Length > 0;
        }

#region Sorting Tasks
        void SortUpdateTasks(TaskInfo[] tasks)
        {
            switch (_sortingOption)
            {
                case SORTING_OPTIONS.AVERAGE:
                    Array.Sort(tasks,
                        (task1, task2) => task2.averageUpdateDuration.CompareTo(task1.averageUpdateDuration));
                    break;
                case SORTING_OPTIONS.MIN:
                    Array.Sort(tasks,
                        (task1, task2) => task2.minUpdateDuration.CompareTo(task1.minUpdateDuration));
                    break;
                case SORTING_OPTIONS.MAX:
                    Array.Sort(tasks,
                        (task1, task2) => task2.maxUpdateDuration.CompareTo(task1.maxUpdateDuration));
                    break;
                case SORTING_OPTIONS.NAME:
                    Array.Sort(tasks,
                        (task1, task2) => String.Compare(task1.taskName, task2.taskName, StringComparison.Ordinal));
                    break;
            }
        }
    }
#endregion
}
#endif