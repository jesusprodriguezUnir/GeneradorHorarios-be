namespace HorariosEscolares.Application.Features.Schools;

public static class SchoolValidationHelper
{
    public static void ValidateWorkingDays(IReadOnlyList<int> workingDays)
    {
        if (workingDays.Count == 0)
            throw new InvalidOperationException("Debe haber al menos un día lectivo.");
        if (workingDays.Distinct().Count() != workingDays.Count)
            throw new InvalidOperationException("Los días lectivos no pueden repetirse.");
    }

    public static void ValidateMonths(IReadOnlyList<int> months)
    {
        if (months.Count == 0)
            throw new InvalidOperationException("Debe haber al menos un mes.");
        if (months.Any(m => m < 1 || m > 12))
            throw new InvalidOperationException("Los meses deben estar entre 1 y 12.");
        if (months.Distinct().Count() != months.Count)
            throw new InvalidOperationException("Los meses no pueden repetirse.");
    }

    public static void ValidateCycleBoundaries(
        TimeOnly morningStart, TimeOnly morningEnd,
        TimeOnly? afternoonStart, TimeOnly? afternoonEnd)
    {
        if (morningStart >= morningEnd)
            throw new InvalidOperationException("La entrada de la mañana debe ser anterior a la salida de la mañana.");
        if (afternoonStart.HasValue && !afternoonEnd.HasValue)
            throw new InvalidOperationException("Si se indica inicio de tarde, debe indicarse también la salida de tarde.");
        if (!afternoonStart.HasValue && afternoonEnd.HasValue)
            throw new InvalidOperationException("Si se indica salida de tarde, debe indicarse también el inicio de tarde.");
        if (afternoonStart.HasValue && afternoonEnd.HasValue)
        {
            if (afternoonStart.Value < morningEnd)
                throw new InvalidOperationException("La entrada de la tarde no puede ser anterior a la salida de la mañana.");
            if (afternoonStart.Value >= afternoonEnd.Value)
                throw new InvalidOperationException("La entrada de la tarde debe ser anterior a la salida de la tarde.");
        }
    }
}
