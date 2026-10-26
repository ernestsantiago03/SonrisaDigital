using Xunit;

// Las pruebas de integración comparten una base temporal y crean y eliminan
// objetos de SQL Server. Se ejecutan en serie para que el resultado sea estable
// y para no competir por la misma base.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
