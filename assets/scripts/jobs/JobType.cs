/// <summary>
/// The stable set of jobs the game can request from <see cref="JobFactory"/>.
/// </summary>
// Each member comes with its JobFactory.CreateDefault() map entry
public enum JobType
{
    FillDocument,
    PrintReport,
}
