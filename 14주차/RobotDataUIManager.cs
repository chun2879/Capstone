using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 여러 Run 저장을 위한 데이터 클래스
[System.Serializable]
public class SimulationRun
{
    public string name;
    public float cubeMass;
    public List<RecordedFrame> frames = new List<RecordedFrame>();
}

public class RobotDataUIManager : MonoBehaviour
{
    [Header("Controller")]
    public PickAndPlaceController pickController;

    [Header("Playback UI")]
    public Button playButton;
    public Button resetButton;
    public Slider timeSlider;
    public TMP_Text timeLabel;

    [Header("Joint Analysis UI")]
    public TMP_Dropdown jointDropdown;
    public GameObject jointGraphPanel;
    public JointGraphView jointGraphView;
    public TMP_Text jointTitleLabel;

    [Header("Run Comparison UI (14주차)")]
    public TMP_Dropdown runDropdown;      // 저장된 Run 선택
    public Button saveRunButton;          // 현재 Run 저장 버튼
    public Button clearRunsButton;        // Run 목록 초기화 버튼

    [Header("Cube Mass UI")]
    public TMP_InputField cubeMassInput;  // 키보드 입력
    public Button cubeMassApplyButton;    // Apply 버튼
    public TMP_Text cubeMassLabel;        // 현재 질량 표시 (옵션)

    public bool IsPlaying = false;

    private bool ignoreSlider = false;
    private float simTime = 0f;

    private int currentJointIndex = 0;

    // 저장된 Run 리스트
    [SerializeField]
    private List<SimulationRun> savedRuns = new List<SimulationRun>();


    void Start()
    {
        //  재생 / 리셋 버튼
        if (playButton != null)
            playButton.onClick.AddListener(OnPlay);
        else
            Debug.LogWarning("[RobotDataUIManager] playButton is not assigned.");

        if (resetButton != null)
            resetButton.onClick.AddListener(OnReset);
        else
            Debug.LogWarning("[RobotDataUIManager] resetButton is not assigned.");

        if (timeSlider != null)
        {
            timeSlider.onValueChanged.AddListener(OnSlider);
            timeSlider.minValue = 0f;
            timeSlider.maxValue = 0f;
            timeSlider.value = 0f;
            timeSlider.interactable = false;
        }
        else
        {
            Debug.LogWarning("[RobotDataUIManager] timeSlider is not assigned.");
        }

        if (timeLabel != null)
            timeLabel.text = "0.00 s";
        else
            Debug.LogWarning("[RobotDataUIManager] timeLabel is not assigned.");

        // 관절 Dropdown 초기화 
        if (jointDropdown != null &&
            pickController != null &&
            pickController.physicalArmJoints != null)
        {
            jointDropdown.ClearOptions();
            List<TMP_Dropdown.OptionData> options = new List<TMP_Dropdown.OptionData>();

            foreach (var j in pickController.physicalArmJoints)
                options.Add(new TMP_Dropdown.OptionData(j.name));

            jointDropdown.AddOptions(options);
            jointDropdown.onValueChanged.AddListener(OnJointDropdownChanged);
        }
        else
        {
            if (jointDropdown == null)
                Debug.LogWarning("[RobotDataUIManager] jointDropdown is not assigned.");
            if (pickController == null)
                Debug.LogWarning("[RobotDataUIManager] pickController is not assigned (for jointDropdown).");
        }

        if (jointGraphPanel != null)
            jointGraphPanel.SetActive(true);

        // Run Dropdown 초기화
        if (runDropdown != null)
        {
            runDropdown.onValueChanged.AddListener(OnRunDropdownChanged);
            RefreshRunDropdownOptions();
        }
        else
        {
            Debug.LogWarning("[RobotDataUIManager] runDropdown is not assigned.");
        }

        if (saveRunButton != null)
            saveRunButton.onClick.AddListener(OnClick_SaveRun);
        else
            Debug.LogWarning("[RobotDataUIManager] saveRunButton is not assigned.");

        if (clearRunsButton != null)
            clearRunsButton.onClick.AddListener(OnClick_ClearRuns);
        else
            Debug.LogWarning("[RobotDataUIManager] clearRunsButton is not assigned.");

        // 큐브 질량 UI 초기화
        if (cubeMassApplyButton != null)
            cubeMassApplyButton.onClick.AddListener(OnClick_ApplyCubeMass);
        else
            Debug.LogWarning("[RobotDataUIManager] cubeMassApplyButton is not assigned.");

        if (cubeMassInput != null)
        {
            cubeMassInput.onEndEdit.AddListener(OnCubeMassInputEndEdit);
        }
        else
        {
            Debug.LogWarning("[RobotDataUIManager] cubeMassInput is not assigned.");
        }

        // 씬에 있는 큐브 질량으로 UI 초기화
        UpdateCubeMassUIFromScene();
    }

    // ============================ 재생 / 리셋 =============================
    public void OnPlay()
    {
        if (pickController == null)
        {
            Debug.LogWarning("[RobotDataUIManager] OnPlay called but pickController is null.");
            return;
        }

        simTime = 0f;
        IsPlaying = true;

        if (timeSlider != null)
        {
            timeSlider.interactable = false;

            ignoreSlider = true;
            timeSlider.value = 0f;
            ignoreSlider = false;
        }

        if (timeLabel != null)
            timeLabel.text = "0.00 s";

        pickController.PlaySimulation();
    }

    public void OnReset()
    {
        IsPlaying = false;
        simTime = 0f;

        if (timeSlider != null)
        {
            ignoreSlider = true;
            timeSlider.value = 0f;
            timeSlider.maxValue = 0f;
            timeSlider.interactable = false;
            ignoreSlider = false;
        }

        if (timeLabel != null)
            timeLabel.text = "0.00 s";

        // RunDropdown은 항상 Current Run(0)으로 돌려놓기
        if (runDropdown != null)
            runDropdown.value = 0;

        if (pickController == null)
        {
            Debug.LogWarning("[RobotDataUIManager] OnReset called but pickController is null.");
            return;
        }

        pickController.ResetSimulation();

        if (pickController.recorder == null || !pickController.recorder.HasData)
        {
            if (jointGraphView != null)
                jointGraphView.SetData(new List<float>(), new List<float>(), new List<float>());
            return;
        }

        RebuildJointGraph();
    }

    // ============================ 플레이 중 시간 갱신 =============================
    void Update()
    {
        if (!IsPlaying) return;

        simTime += Time.deltaTime;

        if (timeSlider != null)
        {
            ignoreSlider = true;
            timeSlider.value = simTime;
            ignoreSlider = false;
        }

        if (timeLabel != null)
            timeLabel.text = simTime.ToString("F2") + " s";

        if (jointGraphView != null)
            jointGraphView.SetHighlightTime(simTime);
    }

    // ============================ 슬라이더로 재생 위치 조정 =============================
    public void OnSlider(float v)
    {
        if (ignoreSlider) return;

        if (pickController != null &&
            pickController.recorder != null &&
            pickController.recorder.HasData)
        {
            IsPlaying = false;

            simTime = v;
            pickController.ApplyRecordedState(v);

            if (timeLabel != null)
                timeLabel.text = v.ToString("F2") + " s";

            if (jointGraphView != null)
                jointGraphView.SetHighlightTime(v);
        }
    }

    public void OnSimulationFinished(float maxTime)
    {
        if (timeSlider != null)
        {
            ignoreSlider = true;
            timeSlider.maxValue = maxTime;
            timeSlider.value = maxTime;
            timeSlider.interactable = true;
            ignoreSlider = false;
        }

        IsPlaying = false;

        RebuildJointGraph();
    }

    // ============================ 관절 Dropdown 변경 =============================
    private void OnJointDropdownChanged(int index)
    {
        currentJointIndex = index;
        RebuildJointGraph();
    }

    // ============================ Run 저장 버튼 =============================
    public void OnClick_SaveRun()
    {
        if (pickController == null || pickController.recorder == null)
        {
            Debug.LogWarning("[RobotDataUIManager] SaveRun pressed but pickController or recorder is null.");
            return;
        }

        var recorder = pickController.recorder;

        if (!recorder.HasData)
        {
            Debug.LogWarning("[RobotDataUIManager] SaveRun pressed but recorder has no data.");
            return;
        }

        var frames = recorder.Frames;
        if (frames == null || frames.Count == 0)
        {
            Debug.LogWarning("[RobotDataUIManager] SaveRun: recorder.Frames is empty.");
            return;
        }

        // 부하(큐브) 질량 가져오기
        float cubeMass = 0f;
        if (pickController.cube != null)
        {
            var rb = pickController.cube.GetComponent<Rigidbody>();
            if (rb != null)
                cubeMass = rb.mass;
        }

        // Run 이름 자동생성
        string runName = $"Run {savedRuns.Count + 1}";
        if (cubeMass > 0f)
            runName += $" - {cubeMass:0.#}kg";

        SimulationRun run = new SimulationRun();
        run.name = runName;
        run.cubeMass = cubeMass;

        // RecordedFrame 깊은 복사
        foreach (var f in frames)
            run.frames.Add(f.Clone());

        savedRuns.Add(run);

        RefreshRunDropdownOptions();
    }

    // ============================ Run 목록 초기화 버튼 =============================
    public void OnClick_ClearRuns()
    {
        savedRuns.Clear();
        RefreshRunDropdownOptions();
    }

    // ============================ Run Dropdown 옵션 갱신 =============================
    private void RefreshRunDropdownOptions()
    {
        if (runDropdown == null) return;

        runDropdown.ClearOptions();

        List<TMP_Dropdown.OptionData> opts = new List<TMP_Dropdown.OptionData>();

        // 0번: Current Run
        opts.Add(new TMP_Dropdown.OptionData("Current Run"));

        foreach (var run in savedRuns)
            opts.Add(new TMP_Dropdown.OptionData(run.name));

        runDropdown.AddOptions(opts);
        runDropdown.value = 0;  // 기본은 Current Run
    }

    private void OnRunDropdownChanged(int index)
    {
        RebuildJointGraph();
    }

    // ============================ 큐브 질량 UI 관련 =============================
    private void OnClick_ApplyCubeMass()
    {
        if (cubeMassInput == null) return;
        ApplyCubeMassFromString(cubeMassInput.text);
    }

    private void OnCubeMassInputEndEdit(string text)
    {
        ApplyCubeMassFromString(text);
    }

    private void ApplyCubeMassFromString(string text)
    {
        if (pickController == null || pickController.cube == null)
        {
            Debug.LogWarning("[RobotDataUIManager] ApplyCubeMass called but pickController or cube is null.");
            return;
        }

        if (string.IsNullOrWhiteSpace(text)) return;

        if (!float.TryParse(text, System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out float mass))
        {
            Debug.LogWarning("[RobotDataUIManager] Cube mass parse failed: " + text);
            // 파싱 실패하면 UI를 다시 실제 값으로 되돌림
            UpdateCubeMassUIFromScene();
            return;
        }

        mass = Mathf.Max(0.01f, mass); // 0 이상으로 제한

        var rb = pickController.cube.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.mass = mass;
        }

        UpdateCubeMassUIFromScene();
    }

    private void UpdateCubeMassUIFromScene()
    {
        float mass = 0f;

        if (pickController != null && pickController.cube != null)
        {
            var rb = pickController.cube.GetComponent<Rigidbody>();
            if (rb != null)
                mass = rb.mass;
        }

        if (cubeMassInput != null)
            cubeMassInput.text = mass.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);

        if (cubeMassLabel != null)
            cubeMassLabel.text = $"Current Mass: {mass:F2} kg";
    }

    // ============================ 그래프 재구성 =============================
    private void RebuildJointGraph()
    {
        if (jointGraphView == null)
        {
            Debug.LogWarning("[RobotDataUIManager] RebuildJointGraph called but jointGraphView is null.");
            return;
        }

        if (pickController == null || pickController.recorder == null)
        {
            Debug.LogWarning("[RobotDataUIManager] RebuildJointGraph: pickController or recorder is null.");
            jointGraphView.SetData(new List<float>(), new List<float>(), new List<float>());
            return;
        }

        List<RecordedFrame> frames = null;

        // 0번 → 현재 Run
        if (runDropdown == null || runDropdown.value == 0)
        {
            if (!pickController.recorder.HasData)
            {
                jointGraphView.SetData(new List<float>(), new List<float>(), new List<float>());
                return;
            }
            frames = pickController.recorder.Frames;
        }
        else
        {
            int runIndex = runDropdown.value - 1;  // Run 리스트는 0부터
            if (runIndex < 0 || runIndex >= savedRuns.Count)
            {
                jointGraphView.SetData(new List<float>(), new List<float>(), new List<float>());
                return;
            }
            frames = savedRuns[runIndex].frames;
        }

        if (frames == null || frames.Count == 0)
        {
            jointGraphView.SetData(new List<float>(), new List<float>(), new List<float>());
            return;
        }

        List<float> times = new List<float>();
        List<float> torques = new List<float>();
        List<float> velocities = new List<float>();

        for (int i = 0; i < frames.Count; i++)
        {
            var f = frames[i];
            times.Add(f.time);

            float tVal = 0f;
            if (f.pseudoTorques != null &&
                currentJointIndex < f.pseudoTorques.Count)
                tVal = f.pseudoTorques[currentJointIndex];
            torques.Add(tVal);

            float vVal = 0f;
            if (f.jointVelocities != null &&
                currentJointIndex < f.jointVelocities.Count)
                vVal = f.jointVelocities[currentJointIndex];
            velocities.Add(vVal);
        }

        jointGraphView.SetData(times, torques, velocities);

        if (jointTitleLabel != null &&
            jointDropdown != null &&
            currentJointIndex < jointDropdown.options.Count)
        {
            jointTitleLabel.text = jointDropdown.options[currentJointIndex].text;
        }
    }
}
