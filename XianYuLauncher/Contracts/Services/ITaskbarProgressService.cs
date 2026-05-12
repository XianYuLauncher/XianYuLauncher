namespace XianYuLauncher.Contracts.Services;

public interface ITaskbarProgressService
{
    void ShowProgress(double progress);

    void ClearProgress();
}