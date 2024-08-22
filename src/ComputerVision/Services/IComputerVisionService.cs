namespace ComputerVision.Services;

public interface IComputerVisionService
{
    Task ProcessImage(string FileReference);
}
