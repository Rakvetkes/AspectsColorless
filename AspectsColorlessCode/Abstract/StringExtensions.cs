namespace AspectsColorless.AspectsColorlessCode.Abstract;

public static class StringExtensions
{
    public static string CardImagePath(this string path)
    {
        return Path.Join(MainFile.ModId, "images", "card_portraits", path);
    }
}