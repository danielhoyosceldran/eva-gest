using AwesomeAssertions;
using EvaGest.Helpers;
using Xunit;

namespace EvaGest.Tests.Calculs;

public class SetmanaHelperTests
{
    [Fact] // E-15
    public void Dilluns_dins_de_la_mateixa_setmana_torna_ell_mateix()
    {
        var dilluns = new DateOnly(2026, 9, 7);
        for (int i = 0; i < 7; i++)
            SetmanaHelper.DilluIrsDeLaSetmana(dilluns.AddDays(i)).Should().Be(dilluns);
    }

    [Fact] // E-16
    public void Diumenge_pertany_a_la_setmana_que_comenca_el_dilluns_anterior()
    {
        var diumenge = new DateOnly(2026, 9, 13);
        SetmanaHelper.DilluIrsDeLaSetmana(diumenge).Should().Be(new DateOnly(2026, 9, 7));
    }
}
