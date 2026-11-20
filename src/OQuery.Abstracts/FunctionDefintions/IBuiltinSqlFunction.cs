using System.Collections.Generic;

namespace OQuery;

public interface IBuiltinSqlFunction
{
    IEnumerable<SqlFunctionDefintion> CreateAllFunctions();

    SqlFunctionDefintion Count();
    SqlFunctionDefintion CountOver();
    SqlFunctionDefintion Sum();
    SqlFunctionDefintion SumOver();
    SqlFunctionDefintion Average();
    SqlFunctionDefintion AverageOver();
    SqlFunctionDefintion Maximum();
    SqlFunctionDefintion MaximumOver();
    SqlFunctionDefintion Minimum();
    SqlFunctionDefintion MinimumOver();
    SqlFunctionDefintion NullIf();
    SqlFunctionDefintion Day();
    SqlFunctionDefintion Month();
    SqlFunctionDefintion Year();
    SqlFunctionDefintion DateSub();
    SqlFunctionDefintion DateAdd();
    SqlFunctionDefintion MonthSub();
    SqlFunctionDefintion MonthAdd();
    SqlFunctionDefintion YearSub();
    SqlFunctionDefintion YearAdd();
    SqlFunctionDefintion CountIf();
    SqlFunctionDefintion AverageIf();
    SqlFunctionDefintion SumIf();
    SqlFunctionDefintion MinimumIf();
    SqlFunctionDefintion MaximumIf();
    SqlFunctionDefintion IfNull();
    SqlFunctionDefintion MedicalLossRatio();
    SqlFunctionDefintion Concat();
    SqlFunctionDefintion MonthKey();
    SqlFunctionDefintion YearsTo();
    SqlFunctionDefintion ElapsedYears();
    SqlFunctionDefintion YearsToNow();
    SqlFunctionDefintion MonthsTo();
    SqlFunctionDefintion MonthsToNow();
    SqlFunctionDefintion DaysTo();
    SqlFunctionDefintion DaysToNow();
    SqlFunctionDefintion Lag();
    SqlFunctionDefintion WindowSum();
    SqlFunctionDefintion Rank();
    SqlFunctionDefintion RowNumber();
    SqlFunctionDefintion Any();
    SqlFunctionDefintion NotAny();
}
