using System.Collections.Specialized;
using System.Windows.Controls;
using System.Windows.Threading;

namespace EvaGest.Views.Dialogs;

public partial class SaleDialogView : UserControl
{
    public SaleDialogView()
    {
        InitializeComponent();

        // The lines list scrolls by itself; a line just added (always at the end) is
        // brought into view, so a click on a quick button visibly does something even
        // when the list is already full. Deferred until the new row has been laid out.
        ((INotifyCollectionChanged)LineRows.Items).CollectionChanged += (_, e) =>
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
                Dispatcher.InvokeAsync(LinesScroll.ScrollToEnd, DispatcherPriority.Loaded);
        };
    }
}
