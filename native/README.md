# Темы Taskinator и Win32++

В Taskinator откройте **Настройки → Оформление**. Доступны системная тема и три схемы в стиле Office 2003. Голубая схема используется по умолчанию. Выбор применяется после нажатия **ОК** и сохраняется в `%APPDATA%\Taskinator\lists.xml`.

Начиная с Windows 7 панель инструментов рисует градиент через `Win32xx::CDC::GradientFill`, если в папке приложения есть DLL подходящей разрядности. Windows XP SP3, Windows Vista и сборки без компонентов C++ используют для градиента GDI+. У заголовков списков однотонный фон. Системная тема и режим высокой контрастности используют цвета Windows.

## Сборка с Win32++

В командной строке разработчика Visual Studio с компонентами Desktop development with C++ и средствами сборки .NET Framework 4.0 выполните команду из каталога с `Taskinator.csproj`.

```bat
msbuild Taskinator.csproj /p:Configuration=Release /p:BuildWin32xxTheme=true
```

Сборка копирует в выходной каталог обе DLL и `Win32xx-License.txt`. Для выпуска под Windows XP SP3 соберите проект с `/p:BuildWin32xxTheme=false`.

В каталоге `Win32xx` находятся заголовки из [Win32++ 9.6.1](https://github.com/DavidNash2024/Win32xx/releases/tag/v9.6.1), используемые нативным проектом, и лицензия.
