namespace EvaGest.Models;

/// <summary>Configurable classification for cash-out movements (salaries, taxes,
/// cleaning, rent...), same shape as <see cref="PaymentMethod"/>. Lets the month-close
/// report break expenses down instead of showing one undifferentiated total.</summary>
public class ExpenseCategory
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool Active { get; set; } = true;
}
