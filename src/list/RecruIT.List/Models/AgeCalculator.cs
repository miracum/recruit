namespace RecruIT.List.Models;

internal static class AgeCalculator
{
    public static int Calculate(DateTimeOffset birthDate)
    {
        var today = DateTimeOffset.UtcNow;
        var age = today.Year - birthDate.Year;
        if (birthDate.Date > today.AddYears(-age).Date)
        {
            age--;
        }

        return age;
    }
}
