#include "Win32xx/wxx_wincore.h"

// Both exports are called from the WinForms UI thread while its COM apartment is active.
static Win32xx::CWinApp* app = NULL;

extern "C" int __cdecl DrawThemeGradient(
    HDC hdc, int left, int top, int right, int bottom, COLORREF start, COLORREF end)
{
    try
    {
        if (app == NULL)
        {
            app = new Win32xx::CWinApp();
        }

        RECT bounds = { left, top, right, bottom };
        Win32xx::CDC dc(hdc);
        dc.GradientFill(start, end, bounds, TRUE);
        return 1;
    }
    catch (...)
    {
        return 0;
    }
}

extern "C" void __cdecl EndThemeDrawing()
{
    delete app;
    app = NULL;
}
