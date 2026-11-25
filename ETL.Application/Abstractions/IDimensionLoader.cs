namespace ETL.Application.Abstractions;

public interface IDimensionLoader
{
    Task CargarAsync(CancellationToken cancellationToken);
}
