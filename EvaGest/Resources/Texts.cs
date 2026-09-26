using System.Globalization;
using System.Resources;

namespace EvaGest.Resources;

/// <summary>
/// Every piece of text the user reads. The Catalan wording lives in Texts.resx and the
/// Spanish in Texts.es.resx; which one answers depends on the UI culture that
/// <see cref="Services.AppLanguage"/> sets once at startup.
///
/// One property per string, so both XAML ({x:Static}) and the ViewModels name them the
/// same way and the compiler catches a key that no longer exists.
/// </summary>
public static class Texts
{
    private static readonly ResourceManager Manager =
        new("EvaGest.Resources.Texts", typeof(Texts).Assembly);

    /// <summary>Falls back to the key itself: a missing string must never be an
    /// exception in front of the user.</summary>
    public static string Get(string key)
        => Get(key, CultureInfo.CurrentUICulture);

    /// <summary>The same lookup against an explicit culture. Used by the tests, which
    /// must not move the culture of the process they share with every other test.</summary>
    public static string Get(string key, CultureInfo culture)
        => Manager.GetString(key, culture) ?? key;

    /// <summary>What this exact language stores for the key, with no fall back to the
    /// neutral table. Null means that language is missing the row: only a test has any
    /// business asking, since the app itself always wants the fall back.</summary>
    public static string? GetExact(string key, CultureInfo culture)
        => Manager.GetResourceSet(culture, createIfNotExists: true, tryParents: false)?
                  .GetString(key);

    /// <summary>Every key in the table, so a test can check that both languages answer
    /// for all of them.</summary>
    public static IReadOnlyList<string> Keys =>
    [
        nameof(Pending),
        nameof(Completed),
        nameof(Cancelled),
        nameof(CancelledPlural),
        nameof(NoShow),
        nameof(NoShowPlural),
        nameof(Active),
        nameof(Voided),
        nameof(CashIn),
        nameof(CashOut),
        nameof(CashInPlural),
        nameof(CashOutPlural),
        nameof(PricesVatIncluded),
        nameof(PricesVatExcluded),
        nameof(Service),
        nameof(Services),
        nameof(Product),
        nameof(Products),
        nameof(Other),
        nameof(Monday),
        nameof(Tuesday),
        nameof(Wednesday),
        nameof(Thursday),
        nameof(Friday),
        nameof(Saturday),
        nameof(Sunday),
        nameof(MondayShort),
        nameof(TuesdayShort),
        nameof(WednesdayShort),
        nameof(ThursdayShort),
        nameof(FridayShort),
        nameof(SaturdayShort),
        nameof(SundayShort),
        nameof(NavHome),
        nameof(NavAgenda),
        nameof(NavClients),
        nameof(NavWorkers),
        nameof(NavCatalog),
        nameof(NavSales),
        nameof(NavTill),
        nameof(NavReports),
        nameof(NavSettings),
        nameof(NavHelp),
        nameof(Cancel),
        nameof(Save),
        nameof(SaveAlt),
        nameof(Delete),
        nameof(Edit),
        nameof(Close),
        nameof(Remove),
        nameof(Export),
        nameof(Restore),
        nameof(ToggleActive),
        nameof(Record),
        nameof(Wake),
        nameof(Asleep),
        nameof(RegisterNow),
        nameof(ClearFilters),
        nameof(ExportPeriod),
        nameof(VoidSale),
        nameof(Void),
        nameof(Charge),
        nameof(BackToPending),
        nameof(NewClientButton),
        nameof(NewAppointmentButton),
        nameof(NewSaleButton),
        nameof(NewWorkerButton),
        nameof(NewServiceButton),
        nameof(NewProductButton),
        nameof(NewMethodButton),
        nameof(NewCategoryButton),
        nameof(NewCashInButton),
        nameof(NewCashOutButton),
        nameof(AddServiceButton),
        nameof(AddProductButton),
        nameof(AddCustomLineButton),
        nameof(MarkClosedButton),
        nameof(NewClientTitle),
        nameof(NewAppointmentTitle),
        nameof(NewSaleTitle),
        nameof(Name),
        nameof(Mobile),
        nameof(MobilePrefix),
        nameof(Phone),
        nameof(PhoneOptional),
        nameof(EmailOptional),
        nameof(BirthDateOptional),
        nameof(NotesOptional),
        nameof(Address),
        nameof(Date),
        nameof(Time),
        nameof(Client),
        nameof(Worker),
        nameof(AllWorkers),
        nameof(WorkerOptional),
        nameof(ServiceOptional),
        nameof(CategoryOptional),
        nameof(Category),
        nameof(Status),
        nameof(Type),
        nameof(Concept),
        nameof(Amount),
        nameof(AmountEuros),
        nameof(Price),
        nameof(PriceEuros),
        nameof(Duration),
        nameof(DurationMinutes),
        nameof(DurationMinutesOptional),
        nameof(Vat),
        nameof(VatPercent),
        nameof(VatWithColon),
        nameof(VatPercentage),
        nameof(Base),
        nameof(BaseWithColon),
        nameof(TaxableBase),
        nameof(Quota),
        nameof(Total),
        nameof(TotalUpper),
        nameof(TotalWithColon),
        nameof(Method),
        nameof(PaymentMethod),
        nameof(PaymentMethods),
        nameof(ExpenseCategories),
        nameof(Payment),
        nameof(ColumnActive),
        nameof(Schedule),
        nameof(WeeklySchedule),
        nameof(Morning),
        nameof(Afternoon),
        nameof(Lines),
        nameof(Size),
        nameof(From),
        nameof(To),
        nameof(NotRegistered),
        nameof(SearchByNameOrMobile),
        nameof(ShowSleepingClients),
        nameof(ColorInAgenda),
        nameof(PickTimeInAgenda),
        nameof(ClientSearchPlaceholder),
        nameof(ClientNameAlreadyExists),
        nameof(CopyMondayToWeekdays),
        nameof(Today),
        nameof(PreviousDay),
        nameof(NextDay),
        nameof(ThreeDaysBack),
        nameof(ThreeDaysForward),
        nameof(WeekBack),
        nameof(WeekForward),
        nameof(ShowWeekView),
        nameof(ShowThreeDayView),
        nameof(Yesterday),
        nameof(ThisWeek),
        nameof(ThisMonth),
        nameof(PreviousMonth),
        nameof(Custom),
        nameof(TodayAppointments),
        nameof(AppointmentsToday),
        nameof(SalesToday),
        nameof(ChargedToday),
        nameof(DayBalance),
        nameof(Balance),
        nameof(Movements),
        nameof(PeriodVatBreakdown),
        nameof(SplitVat),
        nameof(NoAppointmentsToday),
        nameof(NoAppointmentsThisDay),
        nameof(NoSaleMatchesFilters),
        nameof(NoWorkersYet),
        nameof(NoWorkersYetDetail),
        nameof(NoClosedDaysYet),
        nameof(Overview),
        nameof(ClientRankings),
        nameof(WorkerRanking),
        nameof(SalesEvolution),
        nameof(ClientOfTheMonth),
        nameof(MostVisits),
        nameof(MostSpend),
        nameof(HighestAverage),
        nameof(NotSeenInAWhile),
        nameof(DaysSuffix),
        nameof(WorkPercentColumn),
        nameof(SalesHandled),
        nameof(IncomeColumn),
        nameof(Visits),
        nameof(TotalSpent),
        nameof(AveragePerVisit),
        nameof(FrequencyDays),
        nameof(Indicators),
        nameof(History),
        nameof(ShopDetails),
        nameof(CalculationMode),
        nameof(DefaultPercent),
        nameof(DefaultVatHint),
        nameof(SplitVatOnMovements),
        nameof(SplitVatOnMovementsHint),
        nameof(SlotSizeHint),
        nameof(DefaultAppointmentDuration),
        nameof(OpeningHours),
        nameof(OpeningHoursHint),
        nameof(ClosedDays),
        nameof(ClosedDaysHint),
        nameof(Backups),
        nameof(BackupHour),
        nameof(BackupHourHint),
        nameof(BackupsToKeep),
        nameof(BackupNowButton),
        nameof(RestoreBackupButton),
        nameof(AccountantExport),
        nameof(AccountantExportHint),
        nameof(NoticesAndSounds),
        nameof(GuestNoticeOption),
        nameof(GuestNoticeHint),
        nameof(ConfirmationSoundOption),
        nameof(SaveDataButton),
        nameof(SaveScheduleButton),
        nameof(SaveOptionsButton),
        nameof(UnregisteredClientAppointment),
        nameof(UnregisteredClientSale),
        nameof(OutsideOpeningHours),
        nameof(PossibleOverlap),
        nameof(WorkerScheduleHint),
        nameof(InactiveWorkerHint),
        nameof(ExportProducesTwoFiles),
        nameof(Language),
        nameof(LanguageCatalan),
        nameof(LanguageSpanish),
        nameof(LanguageHint),
        nameof(LanguageSaved),
        nameof(BackupAutomatic),
        nameof(BackupManual),
        nameof(ColorTeal),
        nameof(ColorMagenta),
        nameof(ColorPlum),
        nameof(ColorOlive),
        nameof(ColorOchre),
        nameof(ColorGraphite),
        nameof(EditClientTitle),
        nameof(NameAndMobileRequired),
        nameof(PhoneInvalid),
        nameof(EmailInvalid),
        nameof(DateInvalid),
        nameof(TimeInvalid),
        nameof(DurationInvalid),
        nameof(Quantity),
        nameof(QuantityInvalid),
        nameof(LineDescriptionRequired),
        nameof(LineAmountTooLarge),
        nameof(NewServiceTitle),
        nameof(EditServiceTitle),
        nameof(ServiceNameRequired),
        nameof(NewProductTitle),
        nameof(EditProductTitle),
        nameof(ProductNameRequired),
        nameof(PriceInvalid),
        nameof(VatOutOfRange),
        nameof(NewMethodTitle),
        nameof(EditMethodTitle),
        nameof(MethodNameRequired),
        nameof(NewCategoryTitle),
        nameof(EditCategoryTitle),
        nameof(CategoryNameRequired),
        nameof(RegisteredClientTag),
        nameof(EditAppointmentTitle),
        nameof(GuestNameOrClientRequired),
        nameof(DeleteAppointmentTitle),
        nameof(DeleteAppointmentMessage),
        nameof(AppointmentHasSale),
        nameof(NewCashInTitle),
        nameof(NewCashOutTitle),
        nameof(AmountMustBePositive),
        nameof(PaymentMethodRequired),
        nameof(ConceptRequired),
        nameof(NewWorkerTitle),
        nameof(EditWorkerTitle),
        nameof(WorkerNameRequired),
        nameof(CheckRedDays),
        nameof(AtLeastOneDayRequired),
        nameof(ExportToBeforeFrom),
        nameof(ChooseDestinationFolder),
        nameof(ExportDoneTitle),
        nameof(ExportDoneMessage),
        nameof(RestoreBackupTitle),
        nameof(ChooseBackupFromList),
        nameof(ConfirmRestoreTitle),
        nameof(ConfirmRestoreMessage),
        nameof(BackupRestoredTitle),
        nameof(BackupRestoredMessage),
        nameof(Sleep),
        nameof(DeleteClientTitle),
        nameof(DeleteClientMessage),
        nameof(DeleteForever),
        nameof(HistoryAppointment),
        nameof(HistorySale),
        nameof(EditSaleTitle),
        nameof(NewSaleFromAppointmentTitle),
        nameof(FromAppointmentOn),
        nameof(AtLeastOneLineRequired),
        nameof(MorningStartAndEndRequired),
        nameof(MorningEndsBeforeStart),
        nameof(AfternoonStartAndEndRequired),
        nameof(AfternoonEndsBeforeStart),
        nameof(AfternoonBeforeMorningEnds),
        nameof(DeleteAppointmentOfMessage),
        nameof(AppointmentNotDeletedHasSale),
        nameof(AppointmentDeleted),
        nameof(NoService),
        nameof(Unassigned),
        nameof(NoSchedule),
        nameof(MinutesShort),
        nameof(TypeService),
        nameof(TypeProduct),
        nameof(TypePaymentMethod),
        nameof(TypeExpenseCategory),
        nameof(ArticleService),
        nameof(ArticleProduct),
        nameof(ArticleMethod),
        nameof(ArticleCategory),
        nameof(UsedInAppointmentsOrSales),
        nameof(UsedInSales),
        nameof(UsedInSalesOrMovements),
        nameof(UsedInMovements),
        nameof(CannotDeactivateTitle),
        nameof(KeepOneActiveMethod),
        nameof(MethodNotDeletedLastActive),
        nameof(DeleteCatalogItemTitle),
        nameof(DeleteCatalogItemMessage),
        nameof(CatalogItemDeactivated),
        nameof(CatalogItemDeleted),
        nameof(BirthdaysToday),
        nameof(AppointmentHasSaleCannotReopen),
        nameof(OverdueAppointmentsNotice),
        nameof(NoSalesThisMonth),
        nameof(ClientOfTheMonthLine),
        nameof(MoreLinesSuffix),
        nameof(ConfirmVoidSaleTitle),
        nameof(ConfirmVoidSaleMessage),
        nameof(DeleteForeverTitle),
        nameof(DeleteSaleTitle),
        nameof(DeleteVoidedSaleMessage),
        nameof(DeleteActiveSaleMessage),
        nameof(DeleteButton),
        nameof(SaleVoidedNotDeleted),
        nameof(SaleDeletedForever),
        nameof(VatIncludedExplanation),
        nameof(VatExcludedExplanation),
        nameof(NoBackupYet),
        nameof(LastBackupLine),
        nameof(DataSaved),
        nameof(VatPercentOutOfRange),
        nameof(DefaultVatSaved),
        nameof(VatModeSwitchToExcluded),
        nameof(VatModeSwitchToIncluded),
        nameof(ConfirmVatModeTitle),
        nameof(ConfirmVatModeMessage),
        nameof(UnderstoodChange),
        nameof(VatModeSaved),
        nameof(VatModeNotSaved),
        nameof(SaleTotalTooLarge),
        nameof(ConfirmDeleteBackupsTitle),
        nameof(ConfirmDeleteBackupsMessage),
        nameof(DeleteBackups),
        nameof(ActiveSalesCount),
        nameof(SlotMinutesOption),
        nameof(DefaultDurationInvalid),
        nameof(ScheduleSavedAllClosed),
        nameof(ScheduleSaved),
        nameof(BackupHourInvalid),
        nameof(KeepAtLeastOneBackup),
        nameof(BackupDoneTitle),
        nameof(BackupDoneMessage),
        nameof(BackupRestoredReopen),
        nameof(DeleteMovementTitle),
        nameof(DeleteMovementMessage),
        nameof(Inactive),
        nameof(MarkInactive),
        nameof(Reactivate),
        nameof(DeleteWorkerTitle),
        nameof(DeleteWorkerMessage),
        nameof(WorkerDeactivated),
        nameof(WorkerDeleted),
        nameof(DayNotWorking),
        nameof(DayScheduleCell),
        nameof(ScheduleAndJoiner),
        nameof(WithWorker),
        nameof(ThisAppointment),
        nameof(NewAppointmentAtTime),
        nameof(WeekRangeSameMonth),
        nameof(WeekRangeAcrossMonths),
        nameof(LongDateFormat),
        nameof(DayMonthFormat),
        nameof(UnexpectedErrorTitle),
        nameof(UnexpectedErrorMessage),
        nameof(DatabaseUnreadableTitle),
        nameof(DatabaseUnreadableMessage),
        nameof(FaqSaleRegisterQuestion),
        nameof(FaqSaleRegisterAnswer),
        nameof(FaqSaleEditQuestion),
        nameof(FaqSaleEditAnswer),
        nameof(FaqSaleCustomLineQuestion),
        nameof(FaqSaleCustomLineAnswer),
        nameof(FaqAppointmentCreateQuestion),
        nameof(FaqAppointmentCreateAnswer),
        nameof(FaqAppointmentCompleteQuestion),
        nameof(FaqAppointmentCompleteAnswer),
        nameof(FaqNoShowQuestion),
        nameof(FaqNoShowAnswer),
        nameof(FaqClientNewQuestion),
        nameof(FaqClientNewAnswer),
        nameof(FaqClientHistoryQuestion),
        nameof(FaqClientHistoryAnswer),
        nameof(FaqServiceEditQuestion),
        nameof(FaqServiceEditAnswer),
        nameof(FaqProductEditQuestion),
        nameof(FaqProductEditAnswer),
        nameof(FaqMovementQuestion),
        nameof(FaqMovementAnswer),
        nameof(FaqBackupQuestion),
        nameof(FaqBackupAnswer),
        nameof(FaqRestoreQuestion),
        nameof(FaqRestoreAnswer),
        nameof(FaqWorkerNewQuestion),
        nameof(FaqWorkerNewAnswer),
        nameof(FaqWorkerHolidayQuestion),
        nameof(FaqWorkerHolidayAnswer),
        nameof(FaqVatIncludedQuestion),
        nameof(FaqVatIncludedAnswer),
        nameof(FaqClosedDayQuestion),
        nameof(FaqClosedDayAnswer),
        nameof(FaqExportQuestion),
        nameof(FaqExportAnswer),
        nameof(ExportSalesHeader),
        nameof(ExportVatHeader),
        nameof(ClosedDayFormat),
        nameof(EmailFormat),
        nameof(ExistingClient),
        nameof(NewClient),
        nameof(BrowseClients),
        nameof(NewClientNamePlaceholder),
        nameof(ClientBrowserTitle),
        nameof(ClientSearchPlaceholderBrowse),
        nameof(ClientBrowserEmpty),
        nameof(ChooseClient),
        nameof(ClientNotPicked),
        nameof(NavOwnerMode),
        nameof(NavLock),
        nameof(OwnerUnlockTitle),
        nameof(OwnerUnlockIntro),
        nameof(Pin),
        nameof(UnlockButton),
        nameof(ForgotPin),
        nameof(RecoveryCode),
        nameof(RecoveryIntro),
        nameof(CheckRecoveryCode),
        nameof(BackToPin),
        nameof(WrongPin),
        nameof(WrongRecoveryCode),
        nameof(TooManyAttempts),
        nameof(CreateOwnerPinTitle),
        nameof(CreateOwnerPinIntro),
        nameof(CurrentPin),
        nameof(NewPin),
        nameof(RepeatPin),
        nameof(CreatePinButton),
        nameof(ChangePinTitle),
        nameof(ChangePinButton),
        nameof(PinFormatInvalid),
        nameof(PinsDoNotMatch),
        nameof(WrongCurrentPin),
        nameof(RecoveryCodeTitle),
        nameof(RecoveryCodeMessage),
        nameof(RecoveryCodeWritten),
        nameof(OwnerPinSection),
        nameof(OwnerPinSectionHint),
        nameof(OwnerPinChanged),
        nameof(RestoreBringsBackPin),
    ];

    /// <summary>Pendent</summary>
    public static string Pending => Get(nameof(Pending));

    /// <summary>Realitzada</summary>
    public static string Completed => Get(nameof(Completed));

    /// <summary>Cancel·lada</summary>
    public static string Cancelled => Get(nameof(Cancelled));

    /// <summary>Cancel·lades</summary>
    public static string CancelledPlural => Get(nameof(CancelledPlural));

    /// <summary>No assistida</summary>
    public static string NoShow => Get(nameof(NoShow));

    /// <summary>No assistides</summary>
    public static string NoShowPlural => Get(nameof(NoShowPlural));

    /// <summary>Activa</summary>
    public static string Active => Get(nameof(Active));

    /// <summary>Anul·lada</summary>
    public static string Voided => Get(nameof(Voided));

    /// <summary>Entrada</summary>
    public static string CashIn => Get(nameof(CashIn));

    /// <summary>Sortida</summary>
    public static string CashOut => Get(nameof(CashOut));

    /// <summary>Entrades</summary>
    public static string CashInPlural => Get(nameof(CashInPlural));

    /// <summary>Sortides</summary>
    public static string CashOutPlural => Get(nameof(CashOutPlural));

    /// <summary>Preus amb IVA inclòs</summary>
    public static string PricesVatIncluded => Get(nameof(PricesVatIncluded));

    /// <summary>Preus sense IVA</summary>
    public static string PricesVatExcluded => Get(nameof(PricesVatExcluded));

    /// <summary>Servei</summary>
    public static string Service => Get(nameof(Service));

    /// <summary>Serveis</summary>
    public static string Services => Get(nameof(Services));

    /// <summary>Producte</summary>
    public static string Product => Get(nameof(Product));

    /// <summary>Productes</summary>
    public static string Products => Get(nameof(Products));

    /// <summary>Altres</summary>
    public static string Other => Get(nameof(Other));

    /// <summary>Dilluns</summary>
    public static string Monday => Get(nameof(Monday));

    /// <summary>Dimarts</summary>
    public static string Tuesday => Get(nameof(Tuesday));

    /// <summary>Dimecres</summary>
    public static string Wednesday => Get(nameof(Wednesday));

    /// <summary>Dijous</summary>
    public static string Thursday => Get(nameof(Thursday));

    /// <summary>Divendres</summary>
    public static string Friday => Get(nameof(Friday));

    /// <summary>Dissabte</summary>
    public static string Saturday => Get(nameof(Saturday));

    /// <summary>Diumenge</summary>
    public static string Sunday => Get(nameof(Sunday));

    /// <summary>Dl</summary>
    public static string MondayShort => Get(nameof(MondayShort));

    /// <summary>Dt</summary>
    public static string TuesdayShort => Get(nameof(TuesdayShort));

    /// <summary>Dc</summary>
    public static string WednesdayShort => Get(nameof(WednesdayShort));

    /// <summary>Dj</summary>
    public static string ThursdayShort => Get(nameof(ThursdayShort));

    /// <summary>Dv</summary>
    public static string FridayShort => Get(nameof(FridayShort));

    /// <summary>Ds</summary>
    public static string SaturdayShort => Get(nameof(SaturdayShort));

    /// <summary>Dg</summary>
    public static string SundayShort => Get(nameof(SundayShort));

    /// <summary>Inici</summary>
    public static string NavHome => Get(nameof(NavHome));

    /// <summary>Agenda</summary>
    public static string NavAgenda => Get(nameof(NavAgenda));

    /// <summary>Clients</summary>
    public static string NavClients => Get(nameof(NavClients));

    /// <summary>Treballadores</summary>
    public static string NavWorkers => Get(nameof(NavWorkers));

    /// <summary>Catàleg</summary>
    public static string NavCatalog => Get(nameof(NavCatalog));

    /// <summary>Vendes</summary>
    public static string NavSales => Get(nameof(NavSales));

    /// <summary>Caixa</summary>
    public static string NavTill => Get(nameof(NavTill));

    /// <summary>Informes</summary>
    public static string NavReports => Get(nameof(NavReports));

    /// <summary>Configuració</summary>
    public static string NavSettings => Get(nameof(NavSettings));

    /// <summary>Ajuda</summary>
    public static string NavHelp => Get(nameof(NavHelp));

    /// <summary>Cancel·lar</summary>
    public static string Cancel => Get(nameof(Cancel));

    /// <summary>Guardar</summary>
    public static string Save => Get(nameof(Save));

    /// <summary>Desar</summary>
    public static string SaveAlt => Get(nameof(SaveAlt));

    /// <summary>Eliminar</summary>
    public static string Delete => Get(nameof(Delete));

    /// <summary>Editar</summary>
    public static string Edit => Get(nameof(Edit));

    /// <summary>Tancar</summary>
    public static string Close => Get(nameof(Close));

    /// <summary>Treure</summary>
    public static string Remove => Get(nameof(Remove));

    /// <summary>Exportar</summary>
    public static string Export => Get(nameof(Export));

    /// <summary>Restaurar</summary>
    public static string Restore => Get(nameof(Restore));

    /// <summary>Activar/Desactivar</summary>
    public static string ToggleActive => Get(nameof(ToggleActive));

    /// <summary>Fitxa</summary>
    public static string Record => Get(nameof(Record));

    /// <summary>Despertar</summary>
    public static string Wake => Get(nameof(Wake));

    /// <summary>Adormit</summary>
    public static string Asleep => Get(nameof(Asleep));

    /// <summary>Registrar-lo ara</summary>
    public static string RegisterNow => Get(nameof(RegisterNow));

    /// <summary>Guardar-lo igualment</summary>

    /// <summary>Netejar filtres</summary>
    public static string ClearFilters => Get(nameof(ClearFilters));

    /// <summary>Exportar període</summary>
    public static string ExportPeriod => Get(nameof(ExportPeriod));

    /// <summary>Anul·lar venda</summary>
    public static string VoidSale => Get(nameof(VoidSale));

    /// <summary>Anul·lar</summary>
    public static string Void => Get(nameof(Void));

    /// <summary>Cobrar</summary>
    public static string Charge => Get(nameof(Charge));

    /// <summary>Tornar a pendent</summary>
    public static string BackToPending => Get(nameof(BackToPending));

    /// <summary>+ Nou client</summary>
    public static string NewClientButton => Get(nameof(NewClientButton));

    /// <summary>+ Nova cita</summary>
    public static string NewAppointmentButton => Get(nameof(NewAppointmentButton));

    /// <summary>+ Nova venda</summary>
    public static string NewSaleButton => Get(nameof(NewSaleButton));

    /// <summary>+ Nova treballadora</summary>
    public static string NewWorkerButton => Get(nameof(NewWorkerButton));

    /// <summary>+ Nou servei</summary>
    public static string NewServiceButton => Get(nameof(NewServiceButton));

    /// <summary>+ Nou producte</summary>
    public static string NewProductButton => Get(nameof(NewProductButton));

    /// <summary>+ Nou mètode</summary>
    public static string NewMethodButton => Get(nameof(NewMethodButton));

    /// <summary>+ Nova categoria</summary>
    public static string NewCategoryButton => Get(nameof(NewCategoryButton));

    /// <summary>+ Entrada</summary>
    public static string NewCashInButton => Get(nameof(NewCashInButton));

    /// <summary>+ Sortida</summary>
    public static string NewCashOutButton => Get(nameof(NewCashOutButton));

    /// <summary>+ Servei</summary>
    public static string AddServiceButton => Get(nameof(AddServiceButton));

    /// <summary>+ Producte</summary>
    public static string AddProductButton => Get(nameof(AddProductButton));

    /// <summary>+ Concepte lliure</summary>
    public static string AddCustomLineButton => Get(nameof(AddCustomLineButton));

    /// <summary>+ Marcar tancat</summary>
    public static string MarkClosedButton => Get(nameof(MarkClosedButton));

    /// <summary>Nou client</summary>
    public static string NewClientTitle => Get(nameof(NewClientTitle));

    /// <summary>Nova cita</summary>
    public static string NewAppointmentTitle => Get(nameof(NewAppointmentTitle));

    /// <summary>Nova venda</summary>
    public static string NewSaleTitle => Get(nameof(NewSaleTitle));

    /// <summary>Nom</summary>
    public static string Name => Get(nameof(Name));

    /// <summary>Mòbil</summary>
    public static string Mobile => Get(nameof(Mobile));

    /// <summary>Mòbil: </summary>
    public static string MobilePrefix => Get(nameof(MobilePrefix));

    /// <summary>Telèfon</summary>
    public static string Phone => Get(nameof(Phone));

    /// <summary>Telèfon (opcional)</summary>
    public static string PhoneOptional => Get(nameof(PhoneOptional));

    /// <summary>Correu electrònic (opcional)</summary>
    public static string EmailOptional => Get(nameof(EmailOptional));

    /// <summary>Data de naixement (opcional)</summary>
    public static string BirthDateOptional => Get(nameof(BirthDateOptional));

    /// <summary>Observacions (opcional)</summary>
    public static string NotesOptional => Get(nameof(NotesOptional));

    /// <summary>Adreça</summary>
    public static string Address => Get(nameof(Address));

    /// <summary>Data</summary>
    public static string Date => Get(nameof(Date));

    /// <summary>Hora</summary>
    public static string Time => Get(nameof(Time));

    /// <summary>Client</summary>
    public static string Client => Get(nameof(Client));

    /// <summary>Treballadora</summary>
    public static string Worker => Get(nameof(Worker));

    /// <summary>Totes</summary>
    public static string AllWorkers => Get(nameof(AllWorkers));

    /// <summary>Treballadora (opcional)</summary>
    public static string WorkerOptional => Get(nameof(WorkerOptional));

    /// <summary>Servei (opcional)</summary>
    public static string ServiceOptional => Get(nameof(ServiceOptional));

    /// <summary>Categoria (opcional)</summary>
    public static string CategoryOptional => Get(nameof(CategoryOptional));

    /// <summary>Categoria</summary>
    public static string Category => Get(nameof(Category));

    /// <summary>Estat</summary>
    public static string Status => Get(nameof(Status));

    /// <summary>Tipus</summary>
    public static string Type => Get(nameof(Type));

    /// <summary>Concepte</summary>
    public static string Concept => Get(nameof(Concept));

    /// <summary>Import</summary>
    public static string Amount => Get(nameof(Amount));

    /// <summary>Import (€)</summary>
    public static string AmountEuros => Get(nameof(AmountEuros));

    /// <summary>Preu</summary>
    public static string Price => Get(nameof(Price));

    /// <summary>Preu (€)</summary>
    public static string PriceEuros => Get(nameof(PriceEuros));

    /// <summary>Durada</summary>
    public static string Duration => Get(nameof(Duration));

    /// <summary>Durada (minuts)</summary>
    public static string DurationMinutes => Get(nameof(DurationMinutes));

    /// <summary>Durada (minuts, opcional)</summary>
    public static string DurationMinutesOptional => Get(nameof(DurationMinutesOptional));

    /// <summary>IVA</summary>
    public static string Vat => Get(nameof(Vat));

    /// <summary>IVA (%)</summary>
    public static string VatPercent => Get(nameof(VatPercent));

    /// <summary>IVA:</summary>
    public static string VatWithColon => Get(nameof(VatWithColon));

    /// <summary>Percentatge d'IVA</summary>
    public static string VatPercentage => Get(nameof(VatPercentage));

    /// <summary>Base</summary>
    public static string Base => Get(nameof(Base));

    /// <summary>Base:</summary>
    public static string BaseWithColon => Get(nameof(BaseWithColon));

    /// <summary>Base imposable</summary>
    public static string TaxableBase => Get(nameof(TaxableBase));

    /// <summary>Quota</summary>
    public static string Quota => Get(nameof(Quota));

    /// <summary>Total</summary>
    public static string Total => Get(nameof(Total));

    /// <summary>TOTAL</summary>
    public static string TotalUpper => Get(nameof(TotalUpper));

    /// <summary>Total:</summary>
    public static string TotalWithColon => Get(nameof(TotalWithColon));

    /// <summary>Mètode</summary>
    public static string Method => Get(nameof(Method));

    /// <summary>Mètode de pagament</summary>
    public static string PaymentMethod => Get(nameof(PaymentMethod));

    /// <summary>Mètodes de pagament</summary>
    public static string PaymentMethods => Get(nameof(PaymentMethods));

    /// <summary>Categories de despesa</summary>
    public static string ExpenseCategories => Get(nameof(ExpenseCategories));

    /// <summary>Pagament</summary>
    public static string Payment => Get(nameof(Payment));

    /// <summary>Actiu</summary>
    public static string ColumnActive => Get(nameof(ColumnActive));

    /// <summary>Horari</summary>
    public static string Schedule => Get(nameof(Schedule));

    /// <summary>Horari setmanal</summary>
    public static string WeeklySchedule => Get(nameof(WeeklySchedule));

    /// <summary>Matí</summary>
    public static string Morning => Get(nameof(Morning));

    /// <summary>Tarda</summary>
    public static string Afternoon => Get(nameof(Afternoon));

    /// <summary>Línies</summary>
    public static string Lines => Get(nameof(Lines));

    /// <summary>Mida</summary>
    public static string Size => Get(nameof(Size));

    /// <summary>Des de</summary>
    public static string From => Get(nameof(From));

    /// <summary>Fins a</summary>
    public static string To => Get(nameof(To));

    /// <summary>(no registrat)</summary>
    public static string NotRegistered => Get(nameof(NotRegistered));

    /// <summary>Cerca per nom o mòbil…</summary>
    public static string SearchByNameOrMobile => Get(nameof(SearchByNameOrMobile));

    /// <summary>Mostrar clients adormits</summary>
    public static string ShowSleepingClients => Get(nameof(ShowSleepingClients));

    /// <summary>Color a l'agenda</summary>
    public static string ColorInAgenda => Get(nameof(ColorInAgenda));

    /// <summary>Tria l'hora a l'agenda</summary>
    public static string PickTimeInAgenda => Get(nameof(PickTimeInAgenda));

    /// <summary>Busca per nom o telèfon</summary>
    public static string ClientSearchPlaceholder => Get(nameof(ClientSearchPlaceholder));

    /// <summary>Sembla que aquest client ja existeix: </summary>
    public static string ClientNameAlreadyExists => Get(nameof(ClientNameAlreadyExists));

    /// <summary>Copiar dilluns a dimarts–divendres</summary>
    public static string CopyMondayToWeekdays => Get(nameof(CopyMondayToWeekdays));

    /// <summary>Avui</summary>
    public static string Today => Get(nameof(Today));

    /// <summary>Dia anterior</summary>
    public static string PreviousDay => Get(nameof(PreviousDay));

    /// <summary>Dia següent</summary>
    public static string NextDay => Get(nameof(NextDay));

    /// <summary>Tres dies enrere</summary>
    public static string ThreeDaysBack => Get(nameof(ThreeDaysBack));

    /// <summary>Tres dies endavant</summary>
    public static string ThreeDaysForward => Get(nameof(ThreeDaysForward));

    /// <summary>Setmana anterior</summary>
    public static string WeekBack => Get(nameof(WeekBack));

    /// <summary>Setmana següent</summary>
    public static string WeekForward => Get(nameof(WeekForward));

    /// <summary>Veure la setmana</summary>
    public static string ShowWeekView => Get(nameof(ShowWeekView));

    /// <summary>Veure 3 dies</summary>
    public static string ShowThreeDayView => Get(nameof(ShowThreeDayView));

    /// <summary>Ahir</summary>
    public static string Yesterday => Get(nameof(Yesterday));

    /// <summary>Aquesta setmana</summary>
    public static string ThisWeek => Get(nameof(ThisWeek));

    /// <summary>Aquest mes</summary>
    public static string ThisMonth => Get(nameof(ThisMonth));

    /// <summary>Mes anterior</summary>
    public static string PreviousMonth => Get(nameof(PreviousMonth));

    /// <summary>Personalitzat</summary>
    public static string Custom => Get(nameof(Custom));

    /// <summary>Cites d'avui</summary>
    public static string TodayAppointments => Get(nameof(TodayAppointments));

    /// <summary>Cites avui</summary>
    public static string AppointmentsToday => Get(nameof(AppointmentsToday));

    /// <summary>Vendes avui</summary>
    public static string SalesToday => Get(nameof(SalesToday));

    /// <summary>Cobrat avui</summary>
    public static string ChargedToday => Get(nameof(ChargedToday));

    /// <summary>Balanç del dia</summary>
    public static string DayBalance => Get(nameof(DayBalance));

    /// <summary>Balanç</summary>
    public static string Balance => Get(nameof(Balance));

    /// <summary>Moviments</summary>
    public static string Movements => Get(nameof(Movements));

    /// <summary>Desglossament d'IVA del període</summary>
    public static string PeriodVatBreakdown => Get(nameof(PeriodVatBreakdown));

    /// <summary>Desglossar IVA</summary>
    public static string SplitVat => Get(nameof(SplitVat));

    /// <summary>Avui no tens cap cita apuntada.</summary>
    public static string NoAppointmentsToday => Get(nameof(NoAppointmentsToday));

    /// <summary>Aquest dia no té cap cita apuntada.</summary>
    public static string NoAppointmentsThisDay => Get(nameof(NoAppointmentsThisDay));

    /// <summary>Cap venda coincideix amb aquests filtres.</summary>
    public static string NoSaleMatchesFilters => Get(nameof(NoSaleMatchesFilters));

    /// <summary>Encara no has donat d'alta cap treballadora.</summary>
    public static string NoWorkersYet => Get(nameof(NoWorkersYet));

    /// <summary>Fins que no n'hi hagi cap amb horari, l'agenda no sap qua...</summary>
    public static string NoWorkersYetDetail => Get(nameof(NoWorkersYetDetail));

    /// <summary>Encara no has marcat cap dia com a tancat.</summary>
    public static string NoClosedDaysYet => Get(nameof(NoClosedDaysYet));

    /// <summary>Visió general</summary>
    public static string Overview => Get(nameof(Overview));

    /// <summary>Rànquings de clients</summary>
    public static string ClientRankings => Get(nameof(ClientRankings));

    /// <summary>Rànquing de treballadores</summary>
    public static string WorkerRanking => Get(nameof(WorkerRanking));

    /// <summary>Evolució de vendes (12 mesos)</summary>
    public static string SalesEvolution => Get(nameof(SalesEvolution));

    /// <summary>Client del mes</summary>
    public static string ClientOfTheMonth => Get(nameof(ClientOfTheMonth));

    /// <summary>Més visites</summary>
    public static string MostVisits => Get(nameof(MostVisits));

    /// <summary>Més despesa</summary>
    public static string MostSpend => Get(nameof(MostSpend));

    /// <summary>Major mitjana</summary>
    public static string HighestAverage => Get(nameof(HighestAverage));

    /// <summary>Fa temps que no vénen</summary>
    public static string NotSeenInAWhile => Get(nameof(NotSeenInAWhile));

    /// <summary> dies</summary>
    public static string DaysSuffix => Get(nameof(DaysSuffix));

    /// <summary>% de treball</summary>
    public static string WorkPercentColumn => Get(nameof(WorkPercentColumn));

    /// <summary>Vendes ateses</summary>
    public static string SalesHandled => Get(nameof(SalesHandled));

    /// <summary>Ingressos</summary>
    public static string IncomeColumn => Get(nameof(IncomeColumn));

    /// <summary>Visites</summary>
    public static string Visits => Get(nameof(Visits));

    /// <summary>Total gastat</summary>
    public static string TotalSpent => Get(nameof(TotalSpent));

    /// <summary>Mitjana / visita</summary>
    public static string AveragePerVisit => Get(nameof(AveragePerVisit));

    /// <summary>Freqüència (dies)</summary>
    public static string FrequencyDays => Get(nameof(FrequencyDays));

    /// <summary>Indicadors</summary>
    public static string Indicators => Get(nameof(Indicators));

    /// <summary>Historial</summary>
    public static string History => Get(nameof(History));

    /// <summary>Dades de la barberia</summary>
    public static string ShopDetails => Get(nameof(ShopDetails));

    /// <summary>Mode de càlcul</summary>
    public static string CalculationMode => Get(nameof(CalculationMode));

    /// <summary>Percentatge per defecte (%)</summary>
    public static string DefaultPercent => Get(nameof(DefaultPercent));

    /// <summary>És el que es proposa en crear un servei o un producte nou...</summary>
    public static string DefaultVatHint => Get(nameof(DefaultVatHint));

    /// <summary>Poder desglossar l'IVA als moviments de caixa</summary>
    public static string SplitVatOnMovements => Get(nameof(SplitVatOnMovements));

    /// <summary>Desactivat, un moviment d'entrada o sortida guarda només ...</summary>
    public static string SplitVatOnMovementsHint => Get(nameof(SplitVatOnMovementsHint));

    /// <summary>Alçada de cada franja de la graella setmanal. Com més pet...</summary>
    public static string SlotSizeHint => Get(nameof(SlotSizeHint));

    /// <summary>Durada d'una cita sense servei (minuts)</summary>
    public static string DefaultAppointmentDuration => Get(nameof(DefaultAppointmentDuration));

    /// <summary>Horari d'obertura</summary>
    public static string OpeningHours => Get(nameof(OpeningHours));

    /// <summary>Quan obre la barberia cada dia. Marca el matí, la tarda o...</summary>
    public static string OpeningHoursHint => Get(nameof(OpeningHoursHint));

    /// <summary>Dies tancats</summary>
    public static string ClosedDays => Get(nameof(ClosedDays));

    /// <summary>Festius i vacances. L'agenda mostra el dia atenuat amb el...</summary>
    public static string ClosedDaysHint => Get(nameof(ClosedDaysHint));

    /// <summary>Còpies de seguretat</summary>
    public static string Backups => Get(nameof(Backups));

    /// <summary>Hora de la còpia automàtica</summary>
    public static string BackupHour => Get(nameof(BackupHour));

    /// <summary>La còpia es fa en obrir l'aplicació, si ja ha passat aque...</summary>
    public static string BackupHourHint => Get(nameof(BackupHourHint));

    /// <summary>Còpies a conservar</summary>
    public static string BackupsToKeep => Get(nameof(BackupsToKeep));

    /// <summary>Fer còpia ara</summary>
    public static string BackupNowButton => Get(nameof(BackupNowButton));

    /// <summary>Restaurar còpia</summary>
    public static string RestoreBackupButton => Get(nameof(RestoreBackupButton));

    /// <summary>Exportació per a l'assessoria</summary>
    public static string AccountantExport => Get(nameof(AccountantExport));

    /// <summary>Genera els fitxers de vendes i IVA d'un període per porta...</summary>
    public static string AccountantExportHint => Get(nameof(AccountantExportHint));

    /// <summary>Avisos i sons</summary>
    public static string NoticesAndSounds => Get(nameof(NoticesAndSounds));

    /// <summary>Avisar quan una cita o venda és per a un client no registrat</summary>
    public static string GuestNoticeOption => Get(nameof(GuestNoticeOption));

    /// <summary>L'avís ofereix registrar-lo al moment, però mai impedeix ...</summary>
    public static string GuestNoticeHint => Get(nameof(GuestNoticeHint));

    /// <summary>So curt de confirmació en cobrar una venda</summary>
    public static string ConfirmationSoundOption => Get(nameof(ConfirmationSoundOption));

    /// <summary>Desar dades</summary>
    public static string SaveDataButton => Get(nameof(SaveDataButton));

    /// <summary>Desar horari</summary>
    public static string SaveScheduleButton => Get(nameof(SaveScheduleButton));

    /// <summary>Desar opcions</summary>
    public static string SaveOptionsButton => Get(nameof(SaveOptionsButton));

    /// <summary>Aquest client no està registrat. Pots apuntar la cita igu...</summary>
    public static string UnregisteredClientAppointment => Get(nameof(UnregisteredClientAppointment));

    /// <summary>Aquest client no està registrat. Pots cobrar igualment, p...</summary>
    public static string UnregisteredClientSale => Get(nameof(UnregisteredClientSale));

    /// <summary>L'hora cau fora de l'horari de la barberia.</summary>
    public static string OutsideOpeningHours => Get(nameof(OutsideOpeningHours));

    /// <summary>Possible solapament: no queda capacitat en aquesta franja.</summary>
    public static string PossibleOverlap => Get(nameof(PossibleOverlap));

    /// <summary>Marca el matí, la tarda o totes dues per cada dia. Un dia...</summary>
    public static string WorkerScheduleHint => Get(nameof(WorkerScheduleHint));

    /// <summary>Una treballadora inactiva conserva el seu horari i tot el...</summary>
    public static string InactiveWorkerHint => Get(nameof(InactiveWorkerHint));

    /// <summary>Es generaran dos fitxers: el llistat de vendes i el desgl...</summary>
    public static string ExportProducesTwoFiles => Get(nameof(ExportProducesTwoFiles));

    /// <summary>Idioma</summary>
    public static string Language => Get(nameof(Language));

    /// <summary>Català</summary>
    public static string LanguageCatalan => Get(nameof(LanguageCatalan));

    /// <summary>Castellà</summary>
    public static string LanguageSpanish => Get(nameof(LanguageSpanish));

    /// <summary>Canvia l'idioma de tota l'aplicació. Cal tancar-la i torn...</summary>
    public static string LanguageHint => Get(nameof(LanguageHint));

    /// <summary>Idioma desat. Tanca i torna a obrir l'aplicació per veure'l.</summary>
    public static string LanguageSaved => Get(nameof(LanguageSaved));

    /// <summary>Automàtica</summary>
    public static string BackupAutomatic => Get(nameof(BackupAutomatic));

    /// <summary>Manual</summary>
    public static string BackupManual => Get(nameof(BackupManual));

    /// <summary>Verd blau</summary>
    public static string ColorTeal => Get(nameof(ColorTeal));

    /// <summary>Magenta</summary>
    public static string ColorMagenta => Get(nameof(ColorMagenta));

    /// <summary>Prunya</summary>
    public static string ColorPlum => Get(nameof(ColorPlum));

    /// <summary>Oliva</summary>
    public static string ColorOlive => Get(nameof(ColorOlive));

    /// <summary>Ocre</summary>
    public static string ColorOchre => Get(nameof(ColorOchre));

    /// <summary>Grafit</summary>
    public static string ColorGraphite => Get(nameof(ColorGraphite));

    /// <summary>Editar client</summary>
    public static string EditClientTitle => Get(nameof(EditClientTitle));

    /// <summary>Cal indicar el nom i el mòbil per guardar.</summary>
    public static string NameAndMobileRequired => Get(nameof(NameAndMobileRequired));

    /// <summary>El telèfon ha de tenir 9 xifres, només números (0-9). Es pod...</summary>
    public static string PhoneInvalid => Get(nameof(PhoneInvalid));

    /// <summary>El correu electrònic no és vàlid. Ha de tenir un @ i un dom...</summary>
    public static string EmailInvalid => Get(nameof(EmailInvalid));

    /// <summary>La data ha de tenir el format dd/mm/aaaa i ha de ser una da...</summary>
    public static string DateInvalid => Get(nameof(DateInvalid));

    /// <summary>L'hora ha de tenir el format hh:mm, amb dos punts i no cap ...</summary>
    public static string TimeInvalid => Get(nameof(TimeInvalid));

    /// <summary>La durada ha de ser un nombre enter de minuts més gran que ...</summary>
    public static string DurationInvalid => Get(nameof(DurationInvalid));

    /// <summary>Quantitat</summary>
    public static string Quantity => Get(nameof(Quantity));

    /// <summary>La quantitat ha de ser un nombre enter més gran que zero, n...</summary>
    public static string QuantityInvalid => Get(nameof(QuantityInvalid));

    /// <summary>Cal indicar el concepte de cada línia de la venda.</summary>
    public static string LineDescriptionRequired => Get(nameof(LineDescriptionRequired));

    /// <summary>L'import d'aquesta línia és massa gran. Revisa la quantitat i el preu.</summary>
    public static string LineAmountTooLarge => Get(nameof(LineAmountTooLarge));

    /// <summary>Nou servei</summary>
    public static string NewServiceTitle => Get(nameof(NewServiceTitle));

    /// <summary>Editar servei</summary>
    public static string EditServiceTitle => Get(nameof(EditServiceTitle));

    /// <summary>Cal indicar el nom del servei per guardar.</summary>
    public static string ServiceNameRequired => Get(nameof(ServiceNameRequired));

    /// <summary>Nou producte</summary>
    public static string NewProductTitle => Get(nameof(NewProductTitle));

    /// <summary>Editar producte</summary>
    public static string EditProductTitle => Get(nameof(EditProductTitle));

    /// <summary>Cal indicar el nom del producte per guardar.</summary>
    public static string ProductNameRequired => Get(nameof(ProductNameRequired));

    /// <summary>El preu no és vàlid.</summary>
    public static string PriceInvalid => Get(nameof(PriceInvalid));

    /// <summary>L'IVA ha de ser un percentatge entre 0 i 100.</summary>
    public static string VatOutOfRange => Get(nameof(VatOutOfRange));

    /// <summary>Nou mètode de pagament</summary>
    public static string NewMethodTitle => Get(nameof(NewMethodTitle));

    /// <summary>Editar mètode de pagament</summary>
    public static string EditMethodTitle => Get(nameof(EditMethodTitle));

    /// <summary>Cal indicar el nom del mètode per guardar.</summary>
    public static string MethodNameRequired => Get(nameof(MethodNameRequired));

    /// <summary>Nova categoria de despesa</summary>
    public static string NewCategoryTitle => Get(nameof(NewCategoryTitle));

    /// <summary>Editar categoria de despesa</summary>
    public static string EditCategoryTitle => Get(nameof(EditCategoryTitle));

    /// <summary>Cal indicar el nom de la categoria per guardar.</summary>
    public static string CategoryNameRequired => Get(nameof(CategoryNameRequired));

    /// <summary>✓ Client registrat</summary>
    public static string RegisteredClientTag => Get(nameof(RegisteredClientTag));

    /// <summary>Editar cita</summary>
    public static string EditAppointmentTitle => Get(nameof(EditAppointmentTitle));

    /// <summary>Cal indicar el nom del convidat o triar un client registrat.</summary>
    public static string GuestNameOrClientRequired => Get(nameof(GuestNameOrClientRequired));


    /// <summary>Eliminar cita?</summary>
    public static string DeleteAppointmentTitle => Get(nameof(DeleteAppointmentTitle));

    /// <summary>La cita s'esborrarà de l'agenda. Això no es pot desfer.  ...</summary>
    public static string DeleteAppointmentMessage => Get(nameof(DeleteAppointmentMessage));

    /// <summary>Aquesta cita té una venda associada i no es pot esborrar....</summary>
    public static string AppointmentHasSale => Get(nameof(AppointmentHasSale));

    /// <summary>Nova entrada</summary>
    public static string NewCashInTitle => Get(nameof(NewCashInTitle));

    /// <summary>Nova sortida</summary>
    public static string NewCashOutTitle => Get(nameof(NewCashOutTitle));

    /// <summary>L'import ha de ser més gran que zero.</summary>
    public static string AmountMustBePositive => Get(nameof(AmountMustBePositive));

    /// <summary>Cal triar un mètode de pagament.</summary>
    public static string PaymentMethodRequired => Get(nameof(PaymentMethodRequired));

    /// <summary>Cal indicar el concepte.</summary>
    public static string ConceptRequired => Get(nameof(ConceptRequired));

    /// <summary>Nova treballadora</summary>
    public static string NewWorkerTitle => Get(nameof(NewWorkerTitle));

    /// <summary>Editar treballadora</summary>
    public static string EditWorkerTitle => Get(nameof(EditWorkerTitle));

    /// <summary>Cal indicar el nom de la treballadora.</summary>
    public static string WorkerNameRequired => Get(nameof(WorkerNameRequired));

    /// <summary>Revisa els dies marcats en vermell.</summary>
    public static string CheckRedDays => Get(nameof(CheckRedDays));

    /// <summary>Indica com a mínim un dia i un horari.</summary>
    public static string AtLeastOneDayRequired => Get(nameof(AtLeastOneDayRequired));

    /// <summary>La data «fins a» ha de ser posterior a «des de».</summary>
    public static string ExportToBeforeFrom => Get(nameof(ExportToBeforeFrom));

    /// <summary>Tria la carpeta de destí</summary>
    public static string ChooseDestinationFolder => Get(nameof(ChooseDestinationFolder));

    /// <summary>Exportació completada</summary>
    public static string ExportDoneTitle => Get(nameof(ExportDoneTitle));

    /// <summary>S'han generat els fitxers a: {0}</summary>
    public static string ExportDoneMessage => Get(nameof(ExportDoneMessage));

    /// <summary>Restaurar còpia de seguretat</summary>
    public static string RestoreBackupTitle => Get(nameof(RestoreBackupTitle));

    /// <summary>Tria una còpia de la llista.</summary>
    public static string ChooseBackupFromList => Get(nameof(ChooseBackupFromList));

    /// <summary>Restaurar aquesta còpia?</summary>
    public static string ConfirmRestoreTitle => Get(nameof(ConfirmRestoreTitle));

    /// <summary>Es reemplaçaran totes les dades actuals per les d'aquesta...</summary>
    public static string ConfirmRestoreMessage => Get(nameof(ConfirmRestoreMessage));

    /// <summary>Còpia restaurada</summary>
    public static string BackupRestoredTitle => Get(nameof(BackupRestoredTitle));

    /// <summary>Les dades s'han restaurat correctament. Tanca i torna a o...</summary>
    public static string BackupRestoredMessage => Get(nameof(BackupRestoredMessage));

    /// <summary>Adormir</summary>
    public static string Sleep => Get(nameof(Sleep));

    /// <summary>Vols eliminar {0}?</summary>
    public static string DeleteClientTitle => Get(nameof(DeleteClientTitle));

    /// <summary>S'esborrarà la seva fitxa i tot el seu historial: {0} cit...</summary>
    public static string DeleteClientMessage => Get(nameof(DeleteClientMessage));

    /// <summary>Eliminar definitivament</summary>
    public static string DeleteForever => Get(nameof(DeleteForever));

    /// <summary>Cita</summary>
    public static string HistoryAppointment => Get(nameof(HistoryAppointment));

    /// <summary>Venda</summary>
    public static string HistorySale => Get(nameof(HistorySale));

    /// <summary>Editar venda</summary>
    public static string EditSaleTitle => Get(nameof(EditSaleTitle));

    /// <summary>Nova venda (des de cita)</summary>
    public static string NewSaleFromAppointmentTitle => Get(nameof(NewSaleFromAppointmentTitle));

    /// <summary>Des de la cita del {0} a les {1}</summary>
    public static string FromAppointmentOn => Get(nameof(FromAppointmentOn));

    /// <summary>Cal afegir almenys una línia per cobrar.</summary>
    public static string AtLeastOneLineRequired => Get(nameof(AtLeastOneLineRequired));

    /// <summary>Cal indicar l'hora d'inici i la de fi del matí.</summary>
    public static string MorningStartAndEndRequired => Get(nameof(MorningStartAndEndRequired));

    /// <summary>El matí ha d'acabar després de començar.</summary>
    public static string MorningEndsBeforeStart => Get(nameof(MorningEndsBeforeStart));

    /// <summary>Cal indicar l'hora d'inici i la de fi de la tarda.</summary>
    public static string AfternoonStartAndEndRequired => Get(nameof(AfternoonStartAndEndRequired));

    /// <summary>La tarda ha d'acabar després de començar.</summary>
    public static string AfternoonEndsBeforeStart => Get(nameof(AfternoonEndsBeforeStart));

    /// <summary>La tarda no pot començar abans que acabi el matí.</summary>
    public static string AfternoonBeforeMorningEnds => Get(nameof(AfternoonBeforeMorningEnds));

    /// <summary>S'esborrarà la cita de {0} del {1} a les {2}. Això no es ...</summary>
    public static string DeleteAppointmentOfMessage => Get(nameof(DeleteAppointmentOfMessage));

    /// <summary>La cita té una venda associada i no s'ha esborrat. Anul·l...</summary>
    public static string AppointmentNotDeletedHasSale => Get(nameof(AppointmentNotDeletedHasSale));

    /// <summary>La cita s'ha eliminat.</summary>
    public static string AppointmentDeleted => Get(nameof(AppointmentDeleted));

    /// <summary>Sense servei</summary>
    public static string NoService => Get(nameof(NoService));

    /// <summary>Sense assignar</summary>
    public static string Unassigned => Get(nameof(Unassigned));

    /// <summary>Sense horari</summary>
    public static string NoSchedule => Get(nameof(NoSchedule));

    /// <summary>{0} min</summary>
    public static string MinutesShort => Get(nameof(MinutesShort));

    /// <summary>servei</summary>
    public static string TypeService => Get(nameof(TypeService));

    /// <summary>producte</summary>
    public static string TypeProduct => Get(nameof(TypeProduct));

    /// <summary>mètode de pagament</summary>
    public static string TypePaymentMethod => Get(nameof(TypePaymentMethod));

    /// <summary>categoria de despesa</summary>
    public static string TypeExpenseCategory => Get(nameof(TypeExpenseCategory));

    /// <summary>El servei</summary>
    public static string ArticleService => Get(nameof(ArticleService));

    /// <summary>El producte</summary>
    public static string ArticleProduct => Get(nameof(ArticleProduct));

    /// <summary>El mètode</summary>
    public static string ArticleMethod => Get(nameof(ArticleMethod));

    /// <summary>La categoria</summary>
    public static string ArticleCategory => Get(nameof(ArticleCategory));

    /// <summary>cites o vendes</summary>
    public static string UsedInAppointmentsOrSales => Get(nameof(UsedInAppointmentsOrSales));

    /// <summary>vendes</summary>
    public static string UsedInSales => Get(nameof(UsedInSales));

    /// <summary>vendes o moviments de caixa</summary>
    public static string UsedInSalesOrMovements => Get(nameof(UsedInSalesOrMovements));

    /// <summary>moviments de caixa</summary>
    public static string UsedInMovements => Get(nameof(UsedInMovements));

    /// <summary>No es pot desactivar</summary>
    public static string CannotDeactivateTitle => Get(nameof(CannotDeactivateTitle));

    /// <summary>Cal mantenir almenys un mètode de pagament actiu per pode...</summary>
    public static string KeepOneActiveMethod => Get(nameof(KeepOneActiveMethod));

    /// <summary>«{0}» no s'ha eliminat: cal mantenir almenys un mètode de...</summary>
    public static string MethodNotDeletedLastActive => Get(nameof(MethodNotDeletedLastActive));

    /// <summary>Eliminar {0}?</summary>
    public static string DeleteCatalogItemTitle => Get(nameof(DeleteCatalogItemTitle));

    /// <summary>S'eliminarà «{0}» del catàleg.  Si ja s'ha fet servir, es...</summary>
    public static string DeleteCatalogItemMessage => Get(nameof(DeleteCatalogItemMessage));

    /// <summary>{0} «{1}» ja apareix en {2}, així que s'ha desactivat en ...</summary>
    public static string CatalogItemDeactivated => Get(nameof(CatalogItemDeactivated));

    /// <summary>{0} «{1}» s'ha eliminat.</summary>
    public static string CatalogItemDeleted => Get(nameof(CatalogItemDeleted));

    /// <summary>Avui fa anys: </summary>
    public static string BirthdaysToday => Get(nameof(BirthdaysToday));

    /// <summary>La cita té una venda associada i no s'ha pogut tornar a p...</summary>
    public static string AppointmentHasSaleCannotReopen => Get(nameof(AppointmentHasSaleCannotReopen));

    /// <summary>Fa més d'una hora que hauria d'haver acabat i encara és p...</summary>
    public static string OverdueAppointmentsNotice => Get(nameof(OverdueAppointmentsNotice));

    /// <summary>Encara no hi ha vendes aquest mes.</summary>
    public static string NoSalesThisMonth => Get(nameof(NoSalesThisMonth));

    /// <summary>{0} · {1} visites · {2}</summary>
    public static string ClientOfTheMonthLine => Get(nameof(ClientOfTheMonthLine));

    /// <summary> + {0} més</summary>
    public static string MoreLinesSuffix => Get(nameof(MoreLinesSuffix));

    /// <summary>Anul·lar venda?</summary>
    public static string ConfirmVoidSaleTitle => Get(nameof(ConfirmVoidSaleTitle));

    /// <summary>La venda de {0} passarà a l'estat Anul·lada. Es manté vis...</summary>
    public static string ConfirmVoidSaleMessage => Get(nameof(ConfirmVoidSaleMessage));

    /// <summary>Esborrar definitivament?</summary>
    public static string DeleteForeverTitle => Get(nameof(DeleteForeverTitle));

    /// <summary>Eliminar venda?</summary>
    public static string DeleteSaleTitle => Get(nameof(DeleteSaleTitle));

    /// <summary>La venda de {0} ja està anul·lada. S'esborrarà del tot, a...</summary>
    public static string DeleteVoidedSaleMessage => Get(nameof(DeleteVoidedSaleMessage));

    /// <summary>La venda de {0} forma part de l'historial, així que passa...</summary>
    public static string DeleteActiveSaleMessage => Get(nameof(DeleteActiveSaleMessage));

    /// <summary>Esborrar</summary>
    public static string DeleteButton => Get(nameof(DeleteButton));

    /// <summary>La venda s'ha anul·lat en lloc d'esborrar-se, per no perd...</summary>
    public static string SaleVoidedNotDeleted => Get(nameof(SaleVoidedNotDeleted));

    /// <summary>La venda s'ha esborrat definitivament.</summary>
    public static string SaleDeletedForever => Get(nameof(SaleDeletedForever));

    /// <summary>Els preus del catàleg ja porten l'IVA inclòs. Un servei d...</summary>
    public static string VatIncludedExplanation => Get(nameof(VatIncludedExplanation));

    /// <summary>Els preus del catàleg són sense IVA. Un servei de 15,00 €...</summary>
    public static string VatExcludedExplanation => Get(nameof(VatExcludedExplanation));

    /// <summary>Encara no s'ha fet cap còpia.</summary>
    public static string NoBackupYet => Get(nameof(NoBackupYet));

    /// <summary>Última còpia: {0} ({1})</summary>
    public static string LastBackupLine => Get(nameof(LastBackupLine));

    /// <summary>Dades desades.</summary>
    public static string DataSaved => Get(nameof(DataSaved));

    /// <summary>El percentatge d'IVA ha de ser un número entre 0 i 100.</summary>
    public static string VatPercentOutOfRange => Get(nameof(VatPercentOutOfRange));

    /// <summary>L'IVA per defecte dels serveis i productes nous serà del ...</summary>
    public static string DefaultVatSaved => Get(nameof(DefaultVatSaved));

    /// <summary>Un servei de 15,00 € passaria a cobrar-se a 18,15 € (15,0...</summary>
    public static string VatModeSwitchToExcluded => Get(nameof(VatModeSwitchToExcluded));

    /// <summary>Un servei de 15,00 € passaria a cobrar-se a 15,00 €, amb ...</summary>
    public static string VatModeSwitchToIncluded => Get(nameof(VatModeSwitchToIncluded));

    /// <summary>Canviar el mode d'IVA?</summary>
    public static string ConfirmVatModeTitle => Get(nameof(ConfirmVatModeTitle));

    /// <summary>Aquest canvi modifica el significat de tots els preus del...</summary>
    public static string ConfirmVatModeMessage => Get(nameof(ConfirmVatModeMessage));

    /// <summary>Ho entenc, canviar</summary>
    public static string UnderstoodChange => Get(nameof(UnderstoodChange));

    /// <summary>Mode d'IVA desat. Les vendes noves el faran servir; les j...</summary>
    public static string VatModeSaved => Get(nameof(VatModeSaved));

    /// <summary>No s'ha pogut desar el mode d'IVA. Torna-ho a provar.</summary>
    public static string VatModeNotSaved => Get(nameof(VatModeNotSaved));

    /// <summary>El total d'aquesta venda és massa gran. Revisa les línies i di...</summary>
    public static string SaleTotalTooLarge => Get(nameof(SaleTotalTooLarge));

    /// <summary>Vols conservar només {0} còpies?...</summary>
    public static string ConfirmDeleteBackupsTitle => Get(nameof(ConfirmDeleteBackupsTitle));

    /// <summary>S'esborraran {0} còpies de seguretat antigues i no es podran r...</summary>
    public static string ConfirmDeleteBackupsMessage => Get(nameof(ConfirmDeleteBackupsMessage));

    /// <summary>Esborrar les antigues...</summary>
    public static string DeleteBackups => Get(nameof(DeleteBackups));

    /// <summary>{0} vendes actives</summary>
    public static string ActiveSalesCount => Get(nameof(ActiveSalesCount));

    /// <summary>{0} minuts</summary>
    public static string SlotMinutesOption => Get(nameof(SlotMinutesOption));

    /// <summary>La durada per defecte ha de ser un número de minuts més g...</summary>
    public static string DefaultDurationInvalid => Get(nameof(DefaultDurationInvalid));

    /// <summary>Horari desat. Amb tots els dies tancats, l'agenda no sabr...</summary>
    public static string ScheduleSavedAllClosed => Get(nameof(ScheduleSavedAllClosed));

    /// <summary>Horari desat. L'agenda ja el fa servir.</summary>
    public static string ScheduleSaved => Get(nameof(ScheduleSaved));

    /// <summary>L'hora ha de tenir el format hh:mm, amb hores de 00 a 23 i ...</summary>
    public static string BackupHourInvalid => Get(nameof(BackupHourInvalid));

    /// <summary>Cal conservar com a mínim una còpia.</summary>
    public static string KeepAtLeastOneBackup => Get(nameof(KeepAtLeastOneBackup));

    /// <summary>Còpia feta</summary>
    public static string BackupDoneTitle => Get(nameof(BackupDoneTitle));

    /// <summary>S'ha creat la còpia de seguretat del {0}.</summary>
    public static string BackupDoneMessage => Get(nameof(BackupDoneMessage));

    /// <summary>S'han recuperat les dades de la còpia. Tanca i torna a ob...</summary>
    public static string BackupRestoredReopen => Get(nameof(BackupRestoredReopen));

    /// <summary>Eliminar moviment?</summary>
    public static string DeleteMovementTitle => Get(nameof(DeleteMovementTitle));

    /// <summary>S'eliminarà el moviment de {0}.</summary>
    public static string DeleteMovementMessage => Get(nameof(DeleteMovementMessage));

    /// <summary>Inactiva</summary>
    public static string Inactive => Get(nameof(Inactive));

    /// <summary>Marcar inactiva</summary>
    public static string MarkInactive => Get(nameof(MarkInactive));

    /// <summary>Reactivar</summary>
    public static string Reactivate => Get(nameof(Reactivate));

    /// <summary>Eliminar treballadora?</summary>
    public static string DeleteWorkerTitle => Get(nameof(DeleteWorkerTitle));

    /// <summary>S'eliminarà «{0}» i el seu horari.  Si ja té cites o vend...</summary>
    public static string DeleteWorkerMessage => Get(nameof(DeleteWorkerMessage));

    /// <summary>«{0}» té cites o vendes registrades, així que s'ha marcat...</summary>
    public static string WorkerDeactivated => Get(nameof(WorkerDeactivated));

    /// <summary>«{0}» s'ha eliminat.</summary>
    public static string WorkerDeleted => Get(nameof(WorkerDeleted));

    /// <summary>{0}: no treballa</summary>
    public static string DayNotWorking => Get(nameof(DayNotWorking));

    /// <summary>{0}: {1}</summary>
    public static string DayScheduleCell => Get(nameof(DayScheduleCell));

    /// <summary> i </summary>
    public static string ScheduleAndJoiner => Get(nameof(ScheduleAndJoiner));

    /// <summary>Amb {0}</summary>
    public static string WithWorker => Get(nameof(WithWorker));

    /// <summary>Aquesta cita</summary>
    public static string ThisAppointment => Get(nameof(ThisAppointment));

    /// <summary>{0} · Nova cita</summary>
    public static string NewAppointmentAtTime => Get(nameof(NewAppointmentAtTime));

    /// <summary>{0} – {1} de {2}</summary>
    public static string WeekRangeSameMonth => Get(nameof(WeekRangeSameMonth));

    /// <summary>{0} – {1}</summary>
    public static string WeekRangeAcrossMonths => Get(nameof(WeekRangeAcrossMonths));

    /// <summary>dddd, d MMMM 'de' yyyy</summary>
    public static string LongDateFormat => Get(nameof(LongDateFormat));

    /// <summary>dddd d 'de' MMMM</summary>
    public static string DayMonthFormat => Get(nameof(DayMonthFormat));

    /// <summary>No s'ha pogut completar l'operació</summary>
    public static string UnexpectedErrorTitle => Get(nameof(UnexpectedErrorTitle));

    /// <summary>S'ha produït un error inesperat. L'operació no s'ha pogut...</summary>
    public static string UnexpectedErrorMessage => Get(nameof(UnexpectedErrorMessage));

    /// <summary>No s'han pogut obrir les dades</summary>
    public static string DatabaseUnreadableTitle => Get(nameof(DatabaseUnreadableTitle));

    /// <summary>El fitxer de dades no es pot llegir. Pot ser que estigui ...</summary>
    public static string DatabaseUnreadableMessage => Get(nameof(DatabaseUnreadableMessage));

    /// <summary>Com registro una venda?</summary>
    public static string FaqSaleRegisterQuestion => Get(nameof(FaqSaleRegisterQuestion));

    /// <summary>Ves a Vendes o a l'Inici i prem «+ Nova venda». Tria el c...</summary>
    public static string FaqSaleRegisterAnswer => Get(nameof(FaqSaleRegisterAnswer));

    /// <summary>Com edito o anul·lo una venda?</summary>
    public static string FaqSaleEditQuestion => Get(nameof(FaqSaleEditQuestion));

    /// <summary>A la llista de vendes, prem «Editar» per canviar-ne les d...</summary>
    public static string FaqSaleEditAnswer => Get(nameof(FaqSaleEditAnswer));

    /// <summary>Com afegeixo un concepte personalitzat a una venda?</summary>
    public static string FaqSaleCustomLineQuestion => Get(nameof(FaqSaleCustomLineQuestion));

    /// <summary>Dins del diàleg de venda, prem «+ Concepte lliure» i escr...</summary>
    public static string FaqSaleCustomLineAnswer => Get(nameof(FaqSaleCustomLineAnswer));

    /// <summary>Com creo una cita?</summary>
    public static string FaqAppointmentCreateQuestion => Get(nameof(FaqAppointmentCreateQuestion));

    /// <summary>A l'Agenda o a l'Inici, prem «+ Nova cita». Indica el cli...</summary>
    public static string FaqAppointmentCreateAnswer => Get(nameof(FaqAppointmentCreateAnswer));

    /// <summary>Com marco una cita com a realitzada?</summary>
    public static string FaqAppointmentCompleteQuestion => Get(nameof(FaqAppointmentCompleteQuestion));

    /// <summary>A la taula de cites, cada fila pendent té els botons Real...</summary>
    public static string FaqAppointmentCompleteAnswer => Get(nameof(FaqAppointmentCompleteAnswer));

    /// <summary>Què vol dir «No assistida»?</summary>
    public static string FaqNoShowQuestion => Get(nameof(FaqNoShowQuestion));

    /// <summary>Que el client no s'ha presentat a la cita. No genera cap ...</summary>
    public static string FaqNoShowAnswer => Get(nameof(FaqNoShowAnswer));

    /// <summary>Com afegeixo un client nou?</summary>
    public static string FaqClientNewQuestion => Get(nameof(FaqClientNewQuestion));

    /// <summary>A Clients, prem «+ Nou client» i omple el nom i el mòbil,...</summary>
    public static string FaqClientNewAnswer => Get(nameof(FaqClientNewAnswer));

    /// <summary>Com consulto l'historial d'un client?</summary>
    public static string FaqClientHistoryQuestion => Get(nameof(FaqClientHistoryQuestion));

    /// <summary>A la llista de clients, prem «Fitxa» per veure les seves ...</summary>
    public static string FaqClientHistoryAnswer => Get(nameof(FaqClientHistoryAnswer));

    /// <summary>Com afegeixo o modifico un servei?</summary>
    public static string FaqServiceEditQuestion => Get(nameof(FaqServiceEditQuestion));

    /// <summary>A Catàleg, al bloc Serveis, prem «+ Nou servei» o «Editar...</summary>
    public static string FaqServiceEditAnswer => Get(nameof(FaqServiceEditAnswer));

    /// <summary>Com afegeixo o modifico un producte?</summary>
    public static string FaqProductEditQuestion => Get(nameof(FaqProductEditQuestion));

    /// <summary>Igual que els serveis, però al bloc Productes de la matei...</summary>
    public static string FaqProductEditAnswer => Get(nameof(FaqProductEditAnswer));

    /// <summary>Com registro una entrada o sortida de caixa?</summary>
    public static string FaqMovementQuestion => Get(nameof(FaqMovementQuestion));

    /// <summary>A Caixa, prem «+ Entrada» o «+ Sortida», indica l'import,...</summary>
    public static string FaqMovementAnswer => Get(nameof(FaqMovementAnswer));

    /// <summary>Com faig una còpia de seguretat?</summary>
    public static string FaqBackupQuestion => Get(nameof(FaqBackupQuestion));

    /// <summary>A Configuració, bloc «Còpies de seguretat», prem «Fer còp...</summary>
    public static string FaqBackupAnswer => Get(nameof(FaqBackupAnswer));

    /// <summary>Com recupero una còpia de seguretat?</summary>
    public static string FaqRestoreQuestion => Get(nameof(FaqRestoreQuestion));

    /// <summary>A Configuració, prem «Restaurar còpia», tria-la de la lli...</summary>
    public static string FaqRestoreAnswer => Get(nameof(FaqRestoreAnswer));

    /// <summary>Com afegeixo una treballadora?</summary>
    public static string FaqWorkerNewQuestion => Get(nameof(FaqWorkerNewQuestion));

    /// <summary>A Treballadores, prem «+ Nova treballadora». Indica el no...</summary>
    public static string FaqWorkerNewAnswer => Get(nameof(FaqWorkerNewAnswer));

    /// <summary>Què passa si una treballadora se'n va de vacances?</summary>
    public static string FaqWorkerHolidayQuestion => Get(nameof(FaqWorkerHolidayQuestion));

    /// <summary>Prem «Marcar inactiva». Conserva l'horari i tot l'histori...</summary>
    public static string FaqWorkerHolidayAnswer => Get(nameof(FaqWorkerHolidayAnswer));

    /// <summary>Què vol dir «preus amb IVA inclòs»?</summary>
    public static string FaqVatIncludedQuestion => Get(nameof(FaqVatIncludedQuestion));

    /// <summary>Que el preu que escrius al catàleg és el que cobres. Si t...</summary>
    public static string FaqVatIncludedAnswer => Get(nameof(FaqVatIncludedAnswer));

    /// <summary>Com marco un festiu o unes vacances?</summary>
    public static string FaqClosedDayQuestion => Get(nameof(FaqClosedDayQuestion));

    /// <summary>A Configuració, bloc «Dies tancats», tria la data, escriu...</summary>
    public static string FaqClosedDayAnswer => Get(nameof(FaqClosedDayAnswer));

    /// <summary>Com exporto les dades per a l'assessoria?</summary>
    public static string FaqExportQuestion => Get(nameof(FaqExportQuestion));

    /// <summary>A Vendes, Caixa o Configuració, prem «Exportar període», ...</summary>
    public static string FaqExportAnswer => Get(nameof(FaqExportAnswer));

    /// <summary>data;hora;client;treballadora;conceptes;metode_pagament;b...</summary>
    public static string ExportSalesHeader => Get(nameof(ExportSalesHeader));

    /// <summary>tipus_iva;base;quota;total</summary>
    public static string ExportVatHeader => Get(nameof(ExportVatHeader));

    /// <summary>Dia tancat: {0}</summary>
    public static string ClosedDayFormat => Get(nameof(ClosedDayFormat));

    /// <summary>Correu: {0}</summary>
    public static string EmailFormat => Get(nameof(EmailFormat));

    /// <summary>Client existent</summary>
    public static string ExistingClient => Get(nameof(ExistingClient));

    /// <summary>Client nou</summary>
    public static string NewClient => Get(nameof(NewClient));

    /// <summary>Veure la llista</summary>
    public static string BrowseClients => Get(nameof(BrowseClients));

    /// <summary>Nom del client nou</summary>
    public static string NewClientNamePlaceholder => Get(nameof(NewClientNamePlaceholder));

    /// <summary>Triar client</summary>
    public static string ClientBrowserTitle => Get(nameof(ClientBrowserTitle));

    /// <summary>Escriu per filtrar per nom o telèfon</summary>
    public static string ClientSearchPlaceholderBrowse => Get(nameof(ClientSearchPlaceholderBrowse));

    /// <summary>Cap client coincideix amb la cerca. Prova amb una altra paraula, o torna enrere i tria Client nou.</summary>
    public static string ClientBrowserEmpty => Get(nameof(ClientBrowserEmpty));

    /// <summary>Triar</summary>
    public static string ChooseClient => Get(nameof(ChooseClient));

    /// <summary>Tria un client de la llista, o canvia a Client nou per escriure'n el nom.</summary>
    public static string ClientNotPicked => Get(nameof(ClientNotPicked));

    /// <summary>Mode propietària</summary>
    public static string NavOwnerMode => Get(nameof(NavOwnerMode));

    /// <summary>Bloquejar</summary>
    public static string NavLock => Get(nameof(NavLock));

    /// <summary>Mode propietària</summary>
    public static string OwnerUnlockTitle => Get(nameof(OwnerUnlockTitle));

    /// <summary>Escriu el PIN per obrir Vendes, Caixa, Informes, Treballadores, Catàleg i Configuració.</summary>
    public static string OwnerUnlockIntro => Get(nameof(OwnerUnlockIntro));

    /// <summary>PIN</summary>
    public static string Pin => Get(nameof(Pin));

    /// <summary>Entrar</summary>
    public static string UnlockButton => Get(nameof(UnlockButton));

    /// <summary>He oblidat el PIN</summary>
    public static string ForgotPin => Get(nameof(ForgotPin));

    /// <summary>Codi de recuperació</summary>
    public static string RecoveryCode => Get(nameof(RecoveryCode));

    /// <summary>Escriu el codi de recuperació que vas apuntar en un paper quan vas crear el PIN. Després en podràs triar un de nou.</summary>
    public static string RecoveryIntro => Get(nameof(RecoveryIntro));

    /// <summary>Comprovar el codi</summary>
    public static string CheckRecoveryCode => Get(nameof(CheckRecoveryCode));

    /// <summary>Tornar al PIN</summary>
    public static string BackToPin => Get(nameof(BackToPin));

    /// <summary>Aquest PIN no és correcte. Torna-ho a provar.</summary>
    public static string WrongPin => Get(nameof(WrongPin));

    /// <summary>Aquest codi no és correcte. Revisa el paper on el vas apuntar.</summary>
    public static string WrongRecoveryCode => Get(nameof(WrongRecoveryCode));

    /// <summary>Massa intents seguits. Espera {0} segons i torna-ho a provar.</summary>
    public static string TooManyAttempts => Get(nameof(TooManyAttempts));

    /// <summary>Crea el PIN de propietària</summary>
    public static string CreateOwnerPinTitle => Get(nameof(CreateOwnerPinTitle));

    /// <summary>Amb aquest PIN només tu podràs obrir Vendes, Caixa, Informes, Treballadores, Catàleg i Configuració. Les treballadores continuaran fent servir Inici, Agenda i Clients.</summary>
    public static string CreateOwnerPinIntro => Get(nameof(CreateOwnerPinIntro));

    /// <summary>PIN actual</summary>
    public static string CurrentPin => Get(nameof(CurrentPin));

    /// <summary>PIN nou (de 4 a 6 xifres)</summary>
    public static string NewPin => Get(nameof(NewPin));

    /// <summary>Repeteix el PIN nou</summary>
    public static string RepeatPin => Get(nameof(RepeatPin));

    /// <summary>Crear el PIN</summary>
    public static string CreatePinButton => Get(nameof(CreatePinButton));

    /// <summary>Canviar el PIN de propietària</summary>
    public static string ChangePinTitle => Get(nameof(ChangePinTitle));

    /// <summary>Canviar el PIN</summary>
    public static string ChangePinButton => Get(nameof(ChangePinButton));

    /// <summary>El PIN ha de tenir de 4 a 6 xifres, sense lletres ni espais.</summary>
    public static string PinFormatInvalid => Get(nameof(PinFormatInvalid));

    /// <summary>Els dos PIN nous no coincideixen. Torna'ls a escriure.</summary>
    public static string PinsDoNotMatch => Get(nameof(PinsDoNotMatch));

    /// <summary>El PIN actual no és correcte. Escriu el que fas servir ara per entrar.</summary>
    public static string WrongCurrentPin => Get(nameof(WrongCurrentPin));

    /// <summary>Codi de recuperació</summary>
    public static string RecoveryCodeTitle => Get(nameof(RecoveryCodeTitle));

    /// <summary>Apunta aquest codi en un paper i guarda'l fora de la botiga. Si algun dia oblides el PIN, és l'única manera de crear-ne un de nou. No es tornarà a mostrar.</summary>
    public static string RecoveryCodeMessage => Get(nameof(RecoveryCodeMessage));

    /// <summary>Ja l'he apuntat</summary>
    public static string RecoveryCodeWritten => Get(nameof(RecoveryCodeWritten));

    /// <summary>PIN de propietària</summary>
    public static string OwnerPinSection => Get(nameof(OwnerPinSection));

    /// <summary>El mode propietària es tanca sol després de 5 minuts sense fer res, o quan prems Bloquejar.</summary>
    public static string OwnerPinSectionHint => Get(nameof(OwnerPinSectionHint));

    /// <summary>PIN canviat. El codi de recuperació del paper continua servint.</summary>
    public static string OwnerPinChanged => Get(nameof(OwnerPinChanged));

    /// <summary>La còpia porta el PIN de propietària que hi havia quan es va fer.</summary>
    public static string RestoreBringsBackPin => Get(nameof(RestoreBringsBackPin));
}
