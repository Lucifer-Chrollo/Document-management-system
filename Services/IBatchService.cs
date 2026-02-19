namespace DocumentManagementSystem.Services;

public interface IBatchService
{
    Task<IEnumerable<string>> GetBatchLabelsAsync(int? categoryId = null);
}
