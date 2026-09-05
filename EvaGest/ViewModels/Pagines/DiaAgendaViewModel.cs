using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using EvaGest.Models;

namespace EvaGest.ViewModels.Pagines;

/// <summary>One column of the weekly grid (pantalles 2.2).</summary>
public partial class DiaAgendaViewModel : ObservableObject
{
    public DateOnly Data { get; init; }
    public bool EsAvui { get; init; }
    public bool Tancat { get; init; }
    public string? MotiuTancat { get; init; }

    public string CapcaleraText => Data.ToString("ddd d/M", new System.Globalization.CultureInfo("ca-ES"));

    public ObservableCollection<Cita> Cites { get; init; } = [];
}
