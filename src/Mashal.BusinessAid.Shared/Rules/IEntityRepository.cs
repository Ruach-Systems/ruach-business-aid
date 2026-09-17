namespace Mashal.BusinessAid.Shared;
public interface IEntityRepository
{
    Task<T?> Find<T>(Guid id)
        where T : Entity;
    Task<T> Required<T>(Guid id)
        where T : Entity;
    Task Save(Entity value);
}
