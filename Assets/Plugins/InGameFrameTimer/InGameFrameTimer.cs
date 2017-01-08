using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

public class InGameFrameTimer : MonoBehaviour {

    private Material _mat;
    private readonly List<Vector3> _startPos = new List<Vector3>();
    private readonly List<Vector3> _middlePos = new List<Vector3>();
    private readonly List<Vector3> _stopPos = new List<Vector3>();
    private readonly List<bool> _gcRanThatFrame = new List<bool>();
    private readonly Stopwatch _renderTime = Stopwatch.StartNew();
    private float _renderTimeLastFrame = 0;
    private long _lastGcAmount;
    public int BottomLeftPosX = 10;
    public int BottomLeftPosY = 40;
    public int GraphLength = 200;
    public float GraphScale = 10;
    public bool ShowGcCollectedAmount = true;
    private int _currentRefreshRate = 60;
    private readonly List<int> _gcCollected = new List<int>();
    private readonly List<int> _gcCollectedPosX = new List<int>();
    public bool DisableVsync = true;
    public bool SetTargetFpsToDoubleMonitorHz = false;
    public bool ActivatedOnF2 = true;
    private long _memUsed = 0;

    public Shader shader;

    void Start() {

        _currentRefreshRate = 30;

        if (DisableVsync) {
            QualitySettings.vSyncCount = 0;
        }

        if (SetTargetFpsToDoubleMonitorHz) {
            Application.targetFrameRate = _currentRefreshRate * 2;
        }
        else {
            Application.targetFrameRate = -1;
        }

        _mat = new Material(shader);

    }

    void OnGUI() {
        if (!ActivatedOnF2) {
            return;
        }

        LabelWithShadow(new Rect(BottomLeftPosX + GraphLength + 5, Screen.height - BottomLeftPosY - 10, 100, 20), (_memUsed / 1000000).ToString() + "Mb");
        LabelWithShadow(new Rect(BottomLeftPosX + GraphLength + 5, Screen.height - (1 + BottomLeftPosY + (1f / (_currentRefreshRate) * 1000) * GraphScale) - 10, 100, 20), _currentRefreshRate + " fps");
        LabelWithShadow(new Rect(BottomLeftPosX + GraphLength + 5, Screen.height - (1 + BottomLeftPosY + (1f / (_currentRefreshRate * 2) * 1000) * GraphScale) - 10, 100, 20), _currentRefreshRate * 2 + " fps");
        LabelWithShadow(new Rect(BottomLeftPosX + GraphLength + 5, Screen.height - (1 + BottomLeftPosY + (1f / (_currentRefreshRate * 4) * 1000) * GraphScale) - 10, 100, 20), _currentRefreshRate * 4 + " fps");
        LabelWithShadow(new Rect(BottomLeftPosX + GraphLength + 5, Screen.height - (1 + BottomLeftPosY + (1f / (_currentRefreshRate * 8) * 1000) * GraphScale) - 10, 100, 20), _currentRefreshRate * 8 + " fps");

        if (ShowGcCollectedAmount) {
            for (int i = 0; i < _gcCollected.Count; i++) {
                int collectedMb = _gcCollected[i];
                LabelWithShadow(new Rect(BottomLeftPosX + _gcCollectedPosX[i], Screen.height - BottomLeftPosY, 100, 20), collectedMb + "Mb");
            }
        }
    }


    void Update() {
        if (Input.GetKeyDown(KeyCode.F2)) {
            ActivatedOnF2 = !ActivatedOnF2;
        }
        if (!ActivatedOnF2) {
            return;
        }

        while (_startPos.Count > GraphLength) {
            _startPos.RemoveAt(0);
            _middlePos.RemoveAt(0);
            _stopPos.RemoveAt(0);
            _gcRanThatFrame.RemoveAt(0);
        }

        for (int i = _gcCollected.Count - 1; i >= 0; i--) {
            _gcCollectedPosX[i]--;
            if (_gcCollectedPosX[i] <= 0) {
                _gcCollected.RemoveAt(i);
                _gcCollectedPosX.RemoveAt(i);
            }
        }


        _memUsed = GC.GetTotalMemory(false);
        if (_memUsed < _lastGcAmount) {
            int lastGcRemovedAmount = (int)((_lastGcAmount - _memUsed) / 1000000);
            if (ShowGcCollectedAmount) {
                _gcCollected.Add(lastGcRemovedAmount);
                _gcCollectedPosX.Add(GraphLength);
            }
            _gcRanThatFrame.Add(true);
        }
        else {
            _gcRanThatFrame.Add(false);
        }
        _lastGcAmount = _memUsed;


        float width = Screen.width;
        float height = Screen.height;

        float msDeltaTime = Time.deltaTime * 1000;

        _startPos.Add(new Vector3((GraphLength + BottomLeftPosX + 0.5f) / width, (BottomLeftPosY + 0.5f) / height));
        _middlePos.Add(new Vector3((GraphLength + BottomLeftPosX + 0.5f) / width, (BottomLeftPosY + 0.5f + (msDeltaTime - _renderTimeLastFrame) * GraphScale) / height));
        _stopPos.Add(new Vector3((GraphLength + BottomLeftPosX + 0.5f) / width, (BottomLeftPosY + 0.5f + msDeltaTime * GraphScale) / height));

    }

    void LateUpdate() {
        if (!ActivatedOnF2) {
            return;
        }
        _renderTime.Start();
    }

    IEnumerator OnPostRender() {
        if (!ActivatedOnF2) {
            yield break;
        }

        yield return new WaitForEndOfFrame();

        float width = Screen.width;
        float heigth = Screen.height;

        GL.PushMatrix();
        _mat.SetPass(0);
        GL.LoadOrtho();
        GL.Begin(GL.LINES);

        for (int i = 0; i < _startPos.Count; i++) {
            Vector3 start = _startPos[i];
            Vector3 middle = _middlePos[i];
            Vector3 stop = _stopPos[i];

            Color greenColor;
            if (_gcRanThatFrame[i]) {
                greenColor = Color.magenta;
            }
            else {
                greenColor = Color.green;
            }

            GL.Color(greenColor);
            GL.Vertex(start);
            GL.Vertex(stop);

            _startPos[i] = new Vector3(start.x - (1 / width), start.y);
            _middlePos[i] = new Vector3(middle.x - (1 / width), middle.y);
            _stopPos[i] = new Vector3(stop.x - (1 / width), stop.y);
        }

        GL.Color(Color.yellow);

        GL.Vertex(new Vector3((BottomLeftPosX + 0.5f) / width, (0.5f + BottomLeftPosY) / heigth));
        GL.Vertex(new Vector3((BottomLeftPosX + GraphLength + 1f) / width, (0.5f + BottomLeftPosY) / heigth));

        GL.Vertex(new Vector3((BottomLeftPosX + 0.5f) / width, (1.5f + BottomLeftPosY + (1f / (_currentRefreshRate * 8) * 1000) * GraphScale) / heigth));
        GL.Vertex(new Vector3((BottomLeftPosX + GraphLength + 1f) / width, (1.5f + BottomLeftPosY + (1f / (_currentRefreshRate * 8) * 1000) * GraphScale) / heigth));

        GL.Vertex(new Vector3((BottomLeftPosX + 0.5f) / width, (1.5f + BottomLeftPosY + (1f / (_currentRefreshRate * 4) * 1000) * GraphScale) / heigth));
        GL.Vertex(new Vector3((BottomLeftPosX + GraphLength + 1f) / width, (1.5f + BottomLeftPosY + (1f / (_currentRefreshRate * 4) * 1000) * GraphScale) / heigth));

        GL.Vertex(new Vector3((BottomLeftPosX + 0.5f) / width, (1.5f + BottomLeftPosY + (1f / (_currentRefreshRate * 2) * 1000) * GraphScale) / heigth));
        GL.Vertex(new Vector3((BottomLeftPosX + GraphLength + 1f) / width, (1.5f + BottomLeftPosY + (1f / (_currentRefreshRate * 2) * 1000) * GraphScale) / heigth));

        GL.Vertex(new Vector3((BottomLeftPosX + 0.5f) / width, (1.5f + BottomLeftPosY + (1f / _currentRefreshRate * 1000) * GraphScale) / heigth));
        GL.Vertex(new Vector3((BottomLeftPosX + GraphLength + 1f) / width, (1.5f + BottomLeftPosY + (1f / _currentRefreshRate * 1000) * GraphScale) / heigth));

        GL.End();
        GL.PopMatrix();

        _renderTime.Stop();
        _renderTimeLastFrame = (float)_renderTime.Elapsed.TotalMilliseconds;
        _renderTime.Reset();

    }

    private void LabelWithShadow(Rect rect, string s) {
        Color oldColor = GUI.color;
        GUI.color = Color.black;
        GUI.Label(new Rect(rect.x + 1, rect.y + 1, rect.width, rect.height), s);
        GUI.color = oldColor;
        GUI.Label(new Rect(rect.x, rect.y, rect.width, rect.height), s);
    }
}
