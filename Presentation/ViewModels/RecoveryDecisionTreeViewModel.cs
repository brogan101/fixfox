using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace HelpDesk.Presentation.ViewModels;

public sealed class RecoveryDecisionTreeViewModel : INotifyPropertyChanged
{
    private readonly Stack<RecoveryDecisionStepKind> _history = [];
    private RecoveryDecisionStepKind _currentStep = RecoveryDecisionStepKind.Step1;

    public ObservableCollection<RecoveryDecisionAnswerOption> Answers { get; } = [];
    public ObservableCollection<RecoveryRecommendationCard> Recommendations { get; } = [];
    public ObservableCollection<string> GuidanceNotes { get; } = [];

    public RecoveryDecisionTreeViewModel()
    {
        ApplyStep(RecoveryDecisionStepKind.Step1);
    }

    public RecoveryDecisionStepKind CurrentStep
    {
        get => _currentStep;
        private set
        {
            if (_currentStep == value)
                return;

            _currentStep = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CurrentStepNumber));
            OnPropertyChanged(nameof(CurrentStepAutomationId));
            OnPropertyChanged(nameof(CurrentQuestion));
            OnPropertyChanged(nameof(CanGoBack));
        }
    }

    public int CurrentStepNumber => CurrentStep switch
    {
        RecoveryDecisionStepKind.Step1 => 1,
        RecoveryDecisionStepKind.Step2 => 2,
        RecoveryDecisionStepKind.Step3Intermittent => 3,
        RecoveryDecisionStepKind.Step3NoStart => 4,
        RecoveryDecisionStepKind.Step2SpecificErrors => 5,
        RecoveryDecisionStepKind.Step2Slowness => 6,
        RecoveryDecisionStepKind.Step2AppsCrashing => 7,
        _ => 1
    };

    public string CurrentStepAutomationId => $"Recovery_Step_{CurrentStepNumber}";

    public string CurrentQuestion => CurrentStep switch
    {
        RecoveryDecisionStepKind.Step1 => "Is Windows starting at all?",
        RecoveryDecisionStepKind.Step2 => "What are you experiencing?",
        RecoveryDecisionStepKind.Step3Intermittent => "Windows starts only sometimes or fails randomly at startup.",
        RecoveryDecisionStepKind.Step3NoStart => "Windows will not start.",
        RecoveryDecisionStepKind.Step2SpecificErrors => "Start with Windows integrity checks because specific OS errors usually mean system files or servicing need attention.",
        RecoveryDecisionStepKind.Step2Slowness => "Start with Windows integrity checks, then move to a clean boot if instability still follows startup.",
        RecoveryDecisionStepKind.Step2AppsCrashing => "Windows is stable, so start with app repair or reset instead of broad OS recovery.",
        _ => ""
    };

    public bool CanGoBack => _history.Count > 0;

    public event PropertyChangedEventHandler? PropertyChanged;

    public void SelectAnswer(string answerKey)
    {
        var nextStep = answerKey switch
        {
            "YesWindowsStarts" => RecoveryDecisionStepKind.Step2,
            "SometimesRandomly" => RecoveryDecisionStepKind.Step3Intermittent,
            "NoWontStart" => RecoveryDecisionStepKind.Step3NoStart,
            "SpecificErrorMessages" => RecoveryDecisionStepKind.Step2SpecificErrors,
            "GeneralSlownessOrInstability" => RecoveryDecisionStepKind.Step2Slowness,
            "AppsCrashingWindowsStable" => RecoveryDecisionStepKind.Step2AppsCrashing,
            _ => CurrentStep
        };

        if (nextStep == CurrentStep)
            return;

        _history.Push(CurrentStep);
        ApplyStep(nextStep);
    }

    public void GoBack()
    {
        if (_history.Count == 0)
            return;

        ApplyStep(_history.Pop(), preserveHistory: true);
    }

    private void ApplyStep(RecoveryDecisionStepKind step, bool preserveHistory = false)
    {
        CurrentStep = step;
        Answers.Clear();
        Recommendations.Clear();
        GuidanceNotes.Clear();

        switch (step)
        {
            case RecoveryDecisionStepKind.Step1:
                Answers.Add(new RecoveryDecisionAnswerOption("YesWindowsStarts", "Yes, Windows starts"));
                Answers.Add(new RecoveryDecisionAnswerOption("SometimesRandomly", "Sometimes / randomly"));
                Answers.Add(new RecoveryDecisionAnswerOption("NoWontStart", "No, Windows won't start"));
                break;

            case RecoveryDecisionStepKind.Step2:
                Answers.Add(new RecoveryDecisionAnswerOption("SpecificErrorMessages", "Specific error messages"));
                Answers.Add(new RecoveryDecisionAnswerOption("GeneralSlownessOrInstability", "General slowness or instability"));
                Answers.Add(new RecoveryDecisionAnswerOption("AppsCrashingWindowsStable", "Apps crashing but Windows is stable"));
                break;

            case RecoveryDecisionStepKind.Step3Intermittent:
                GuidanceNotes.Add("Check Event Viewer or recent receipts for critical errors around the startup time.");
                GuidanceNotes.Add("Use Reliability Monitor to confirm whether the failure started after an update, driver, or recurring app crash.");
                GuidanceNotes.Add("If startup still fails intermittently, move to Advanced startup and use Startup Repair before broader reset options.");
                Recommendations.Add(new RecoveryRecommendationCard(
                    "ReliabilityMonitor",
                    "Reliability Monitor",
                    "Open Windows reliability history and look for critical startup failures around the time the machine stopped starting cleanly.",
                    "No data risk.",
                    "~5 to 10 min",
                    "Open in Windows",
                    "perfmon /rel"));
                Recommendations.Add(new RecoveryRecommendationCard(
                    "AdvancedStartup",
                    "Advanced Startup",
                    "Restart into recovery mode so Windows Startup Repair is available if the next reboot fails again.",
                    "No data loss, but the PC restarts immediately.",
                    "~5 to 15 min",
                    "Restart into Recovery",
                    "shutdown /r /o /f /t 0"));
                break;

            case RecoveryDecisionStepKind.Step3NoStart:
                GuidanceNotes.Add("Use Shift + Restart if you can reach the sign-in screen, or boot from Windows installation media if you cannot.");
                GuidanceNotes.Add("Start with Startup Repair before any reset option.");
                GuidanceNotes.Add("Choose the least destructive reset path that matches the risk you can accept.");
                Recommendations.Add(new RecoveryRecommendationCard(
                    "StartupRepair",
                    "Startup Repair",
                    "Open Windows recovery options and use Startup Repair to repair boot files and startup configuration.",
                    "No data loss.",
                    "~5 to 15 min",
                    "Open Recovery",
                    "ms-settings:recovery"));
                Recommendations.Add(new RecoveryRecommendationCard(
                    "ResetKeepFiles",
                    "Reset This PC (Keep Files)",
                    "Reinstall Windows while keeping user files, but remove installed desktop apps.",
                    "Installed apps will be removed. Your files will be kept.",
                    "~30 to 60 min",
                    "Open Recovery",
                    "ms-settings:recovery"));
                Recommendations.Add(new RecoveryRecommendationCard(
                    "ResetRemoveEverything",
                    "Reset This PC (Remove Everything)",
                    "Wipe the device and reinstall Windows from recovery.",
                    "Everything will be erased.",
                    "~30 to 90 min",
                    "Open Recovery",
                    "ms-settings:recovery"));
                break;

            case RecoveryDecisionStepKind.Step2SpecificErrors:
                GuidanceNotes.Add("Run System File Checker first, then DISM if Windows reports corruption or servicing errors.");
                Recommendations.Add(new RecoveryRecommendationCard(
                    "SystemFileChecker",
                    "Run SFC",
                    "Check and repair protected Windows system files before broader recovery work.",
                    "No data loss.",
                    "~10 to 20 min",
                    "Run repair",
                    "run-sfc"));
                Recommendations.Add(new RecoveryRecommendationCard(
                    "DismRepair",
                    "Run DISM",
                    "Repair the Windows component store if SFC finds corruption or cannot finish cleanly.",
                    "No data loss.",
                    "~10 to 20 min",
                    "Run repair",
                    "run-dism"));
                break;

            case RecoveryDecisionStepKind.Step2Slowness:
                GuidanceNotes.Add("Start with System File Checker so Windows health is confirmed before you chase startup pressure.");
                GuidanceNotes.Add("If the device is still unstable after integrity checks, move to a clean boot to isolate startup conflicts.");
                Recommendations.Add(new RecoveryRecommendationCard(
                    "SystemFileChecker",
                    "Run SFC",
                    "Verify core Windows files before you assume startup apps are the only problem.",
                    "No data loss.",
                    "~10 to 20 min",
                    "Run repair",
                    "run-sfc"));
                Recommendations.Add(new RecoveryRecommendationCard(
                    "CleanBootGuidance",
                    "Open Startup Apps",
                    "Review sign-in apps and background launchers so you can perform a clean-boot style isolation safely.",
                    "No data loss, but apps may stop launching automatically.",
                    "~10 to 15 min",
                    "Open in Windows",
                    "ms-settings:startupapps"));
                break;

            case RecoveryDecisionStepKind.Step2AppsCrashing:
                GuidanceNotes.Add("Windows looks stable, so start with the affected app instead of broader system recovery.");
                Recommendations.Add(new RecoveryRecommendationCard(
                    "AppRepair",
                    "Open Installed Apps",
                    "Go to Installed Apps so you can repair, reset, or uninstall the crashing app.",
                    "App preferences may be reset if you choose Reset.",
                    "~5 to 15 min",
                    "Open in Windows",
                    "ms-settings:appsfeatures"));
                break;
        }

        if (!preserveHistory)
            OnPropertyChanged(nameof(CanGoBack));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public enum RecoveryDecisionStepKind
{
    Step1,
    Step2,
    Step3Intermittent,
    Step3NoStart,
    Step2SpecificErrors,
    Step2Slowness,
    Step2AppsCrashing
}

public sealed record RecoveryDecisionAnswerOption(string Key, string Title)
{
    public string AutomationId => $"Recovery_Answer_{Key}";
}

public sealed record RecoveryRecommendationCard(
    string Key,
    string Title,
    string WhatItWillDo,
    string DataRisk,
    string EstimatedTime,
    string ActionLabel,
    string ActionTarget)
{
    public string AutomationId => $"Recovery_Recommendation_{Key}";
    public string DeepLinkAutomationId => $"Recovery_DeepLink_{Key}";
}
