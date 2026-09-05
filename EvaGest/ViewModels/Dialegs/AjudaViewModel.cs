using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace EvaGest.ViewModels.Dialegs;

/// <summary>One collapsible FAQ entry (pantalles 3.10).</summary>
public record PreguntaFaq(string Seccio, string Pregunta, string Resposta);

/// <summary>
/// Ajuda (FAQ), pantalles 3.10. When opened from a specific page, that page's
/// questions are prioritised — shown first, rather than filtered out, so the
/// answer to something learned in another section is never hidden.
/// </summary>
public partial class AjudaViewModel : DialegViewModelBase
{
    public override string Titol => "Ajuda";

    public ObservableCollection<PreguntaFaq> Preguntes { get; }

    public AjudaViewModel(string? seccioPrioritaria = null)
    {
        var totes = ContingutFaq.Totes;
        Preguntes = new ObservableCollection<PreguntaFaq>(
            seccioPrioritaria is null
                ? totes
                : totes.OrderByDescending(p => p.Seccio == seccioPrioritaria));
    }

    [RelayCommand]
    private void TancarAjuda() => SolicitarTancar(true);
}

/// <summary>The 14 questions from RF-19, grouped by the section they belong to.</summary>
public static class ContingutFaq
{
    public static readonly List<PreguntaFaq> Totes =
    [
        new("Vendes", "Com registro una venda?",
            "Ves a Vendes o a l'Inici i prem «+ Nova venda». Tria el client, afegeix les línies " +
            "(servei, producte o un concepte lliure) i el mètode de pagament, i prem «Cobrar»."),
        new("Vendes", "Com edito o anul·lo una venda?",
            "A la llista de vendes, prem «Editar» per canviar-ne les dades, o «Anul·lar» per " +
            "invalidar-la sense esborrar-la. Una venda anul·lada queda visible a l'historial però " +
            "no compta als totals."),
        new("Vendes", "Com afegeixo un concepte personalitzat a una venda?",
            "Dins del diàleg de venda, prem «+ Concepte lliure» i escriu la descripció i el preu a mà."),
        new("Agenda", "Com creo una cita?",
            "A l'Agenda o a l'Inici, prem «+ Nova cita». Indica el client (o un nom de convidat), " +
            "la data, l'hora i, opcionalment, el servei i la treballadora."),
        new("Agenda", "Com marco una cita com a realitzada?",
            "A la taula de cites, cada fila pendent té els botons Realitzada, Cancel·lada i No " +
            "assistida."),
        new("Agenda", "Què vol dir «No assistida»?",
            "Que el client no s'ha presentat a la cita. No genera cap venda ni compta com a visita " +
            "als indicadors del client."),
        new("Clients", "Com afegeixo un client nou?",
            "A Clients, prem «+ Nou client» i omple el nom i el mòbil, que són els únics camps " +
            "obligatoris."),
        new("Clients", "Com consulto l'historial d'un client?",
            "A la llista de clients, prem «Fitxa» per veure les seves dades, indicadors i " +
            "l'historial complet de cites i vendes."),
        new("Catàleg", "Com afegeixo o modifico un servei?",
            "A Catàleg, al bloc Serveis, prem «+ Nou servei» o «Editar» a la fila que vulguis canviar."),
        new("Catàleg", "Com afegeixo o modifico un producte?",
            "Igual que els serveis, però al bloc Productes de la mateixa pàgina de Catàleg."),
        new("Caixa", "Com registro una entrada o sortida de caixa?",
            "A Caixa, prem «+ Entrada» o «+ Sortida», indica l'import, el mètode de pagament i el " +
            "concepte."),
        new("Configuració", "Com faig una còpia de seguretat?",
            "A Configuració, bloc «Còpies de seguretat», prem «Fer còpia ara». L'aplicació també " +
            "en fa una automàticament cada dia."),
        new("Configuració", "Com recupero una còpia de seguretat?",
            "A Configuració, prem «Restaurar còpia», tria-la de la llista i confirma. Abans de " +
            "restaurar, l'estat actual es guarda igualment per si cal desfer-ho."),
        new("Treballadores", "Com afegeixo una treballadora?",
            "Des de la pàgina Treballadores. De moment aquesta pantalla només mostra la llista; " +
            "el formulari per afegir-ne una de nova és a punt de completar-se."),
        new("Vendes", "Com exporto les dades per a l'assessoria?",
            "A Vendes, Caixa o Configuració, prem «Exportar període», indica les dates i tria la " +
            "carpeta on desar els dos fitxers (vendes i IVA)."),
    ];
}
