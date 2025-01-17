
//Prevent AFX defining a second DllMain
extern "C" { int __afxForceUSRDLL; }

#include "afxole.h"
#include "afxext.h"

#include "WindowUtils.h"

#include <shellscalingapi.h>
#pragma comment(lib, "Shcore.lib")

#include <memory>
#include <vector>

namespace ReBuzz
{
    namespace NativeMachineFramework
    {
        static BOOL CALLBACK GetChildwindowsCallback(HWND hwnd, LPARAM lparam)
        {
            std::vector<HWND>* wndlist = reinterpret_cast<std::vector<HWND> *>(lparam);
            wndlist->push_back(hwnd);
            return TRUE;
        }

        __declspec(dllexport) void WindowUtils::RepositionAndResizeChildControls(HWND parent, double scalingFactor)
        {
            char classname[256];
            GetClassNameA(parent, classname, 255);
           
            RECT parentRect;
            GetWindowRect(parent, &parentRect);

            PROCESS_DPI_AWARENESS dpi = (PROCESS_DPI_AWARENESS)-1000;
            GetProcessDpiAwareness(GetCurrentProcess(), &dpi);

            UINT parentDpi = GetDpiForWindow(parent);
            UINT dpiX, dpiY;
            HMONITOR hMonitor = MonitorFromWindow(parent, MONITOR_DEFAULTTONEAREST);
            HRESULT hr = GetDpiForMonitor(hMonitor, MDT_EFFECTIVE_DPI, &dpiX, &dpiY);

            DWORD style = GetWindowLong(parent, GWL_STYLE);
            DWORD exstyle = GetWindowLong(parent, GWL_EXSTYLE);
            AdjustWindowRectExForDpi(&parentRect, style, FALSE, exstyle, dpiY);

            
            //Get child windows and reposition and resize them
            std::vector<HWND> wndList;
            EnumChildWindows(parent, GetChildwindowsCallback, reinterpret_cast<LPARAM>(&wndList));
            for (const auto& hwnd : wndList)
            {
                RECT controlRect;
                GetWindowRect(hwnd, &controlRect);
                //GetClientRect(hwnd, &controlRect);
                GetClassNameA(hwnd, classname, 255);
                MapWindowPoints(HWND_DESKTOP, parent, (LPPOINT)&controlRect, 2);


                //int newX = controlRect.left - parentRect.left;
                int newX = controlRect.left;
                newX *= scalingFactor;
                //int newY = controlRect.top - parentRect.top;
                int newY = controlRect.top;
                int newWidth = controlRect.right - controlRect.left;
                newWidth *= scalingFactor;
                int newHeight = controlRect.bottom - controlRect.top;

                style = GetWindowLong(hwnd, GWL_STYLE);
                exstyle = GetWindowLong(hwnd, GWL_EXSTYLE);
                bool ischild = (style & WS_CHILD);

                RECT newRect;
                newRect.left = newX;
                newRect.top = newY;
                newRect.right = newX + newWidth;
                newRect.bottom = newY + newHeight;
                AdjustWindowRectExForDpi(&newRect, style, FALSE, exstyle, dpiY);

                if (ischild)
                {
                    //SetWindowPos(hwnd, NULL, newX, newY, newWidth, newHeight, SWP_NOZORDER);
                    MoveWindow(hwnd, newRect.left, newRect.top, newRect.right - newRect.left, newRect.bottom - newRect.top, FALSE);
                }
                
                
                //MoveWindow(hwnd, newX, newY, newWidth, newHeight, TRUE);
            }

        }

        __declspec(dllexport) double WindowUtils::GetScalingFactor(HWND hwnd)
        {
            //Which monitor is this window on?
            HMONITOR hMonitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);

            //Get scaling factor
            UINT dpiX, dpiY;
            HRESULT hr = GetDpiForMonitor(hMonitor, MDT_EFFECTIVE_DPI, &dpiX, &dpiY);
            double scalingFactor = dpiY / 96.0;
            if (FAILED(hr))
                return 1.0f;

            return scalingFactor;
        }

        __declspec(dllexport) double WindowUtils::GetToolbarScalingFactor(HWND hwnd)
        {
            return GetScalingFactor(hwnd) * 1.5;
        }

        __declspec(dllexport) void WindowUtils::ResizeImageListForCurrentDPI(void * toolbarPtr)
        {
            CToolBar* toolbar = reinterpret_cast<CToolBar*>(toolbarPtr);
            if (toolbar->m_hWnd == NULL)
                return;

           
            HIMAGELIST hImgList = (HIMAGELIST)SendMessage(toolbar->m_hWnd, TB_GETIMAGELIST, 0, 0);
            if (hImgList == NULL)
                return;
            
            double scalingFactor = GetScalingFactor(toolbar->m_hWnd);
            

            HDC toolbarDC = GetDC(toolbar->m_hWnd);
            int imageCount = ImageList_GetImageCount(hImgList);
            int newImageWidth = 0;
            int newImageHeight = 0;
            LRESULT btnSize = SendMessage(toolbar->m_hWnd, TB_GETBUTTONSIZE, 0, 0);
            std::vector<HBITMAP> newImages;
            for (int img = 0; img < imageCount; ++img)
            {

                //Extract image info
                IMAGEINFO info = { 0 };
                ImageList_GetImageInfo(hImgList, img, &info);
                int bitsPerPixel = 0;
                int imgWidth = 0;
                int imgHeight = 0;
                int imgSize = 0;
                DIBSECTION dibSection = { 0 };
                GetObject(info.hbmImage, sizeof(dibSection), &dibSection);
                if (dibSection.dsBmih.biBitCount != 0)
                {
                    bitsPerPixel = dibSection.dsBmih.biBitCount;
                    imgWidth = dibSection.dsBmih.biWidth;
                    imgHeight = dibSection.dsBmih.biHeight;
                }
                else
                {
                    bitsPerPixel = dibSection.dsBm.bmBitsPixel;
                    imgHeight = dibSection.dsBm.bmHeight;
                    imgWidth = dibSection.dsBm.bmWidth;
                }

                //Create temporary device contexts to store the image
                HDC srcDC = CreateCompatibleDC(toolbarDC);
                HDC maskDC = CreateCompatibleDC(toolbarDC);
                HDC maskScaleDC = CreateCompatibleDC(toolbarDC);
                HDC scaleImgDC = CreateCompatibleDC(toolbarDC);
               
                //Determine image width and height
                imgWidth = (info.rcImage.right - info.rcImage.left);
                imgHeight = (info.rcImage.bottom - info.rcImage.top);


                BITMAPINFO dibSectionInfo = { 0 };
                dibSectionInfo.bmiHeader.biWidth = imgWidth;
                dibSectionInfo.bmiHeader.biHeight = imgHeight;
                dibSectionInfo.bmiHeader.biBitCount = 32;
                dibSectionInfo.bmiHeader.biPlanes = 1;
                dibSectionInfo.bmiHeader.biCompression = BI_RGB;
                dibSectionInfo.bmiHeader.biSize = sizeof(BITMAPINFO);
                dibSectionInfo.bmiHeader.biSizeImage = ((((dibSectionInfo.bmiHeader.biWidth * (LONG)dibSectionInfo.bmiHeader.biBitCount) + 31) & ~31) >> 3) * dibSectionInfo.bmiHeader.biHeight;
                void* srcBmpDataPtr = NULL;
                HBITMAP hBmpSrcTmp = CreateDIBSection(toolbarDC, &dibSectionInfo, DIB_RGB_COLORS, &srcBmpDataPtr, NULL, 0);
                
                
                //Draw image to source bitmap
                auto oldSrcBmp = SelectObject(srcDC, hBmpSrcTmp);
                ImageList_Draw(hImgList, img, srcDC, 0, 0, ILD_NORMAL);
                SelectObject(srcDC, oldSrcBmp);

                //Draw mask, using white as the background and black foreground
                RECT fillrt;
                void* maskBits;
                HBITMAP hBmpMask = CreateDIBSection(toolbarDC, &dibSectionInfo, DIB_RGB_COLORS, &maskBits, NULL, 0);
                auto oldMaskBmp = SelectObject(maskDC, hBmpMask);
                fillrt.left = 0;
                fillrt.top = 0;
                fillrt.right = imgWidth;
                fillrt.bottom = imgHeight;
                FillRect(maskDC, &fillrt, (HBRUSH)CreateSolidBrush(RGB(255, 255, 255)));
                auto hOldBrush = SelectObject(maskDC, CreateSolidBrush(RGB(0,0,0)));
                ImageList_Draw(hImgList, img, maskDC, 0, 0, ILD_MASK);
                SelectObject(maskDC, hOldBrush);
                SelectObject(maskDC, oldMaskBmp);
                
                //Scale mask
                BITMAPINFO dibScaledInfo = { 0 };
                dibScaledInfo.bmiHeader.biWidth = scalingFactor * (info.rcImage.right - info.rcImage.left);
                dibScaledInfo.bmiHeader.biHeight = scalingFactor * (info.rcImage.bottom - info.rcImage.top);
                dibScaledInfo.bmiHeader.biBitCount = 32;
                dibScaledInfo.bmiHeader.biPlanes = 1;
                dibScaledInfo.bmiHeader.biCompression = BI_RGB;
                dibScaledInfo.bmiHeader.biSize = sizeof(BITMAPINFO);
                dibScaledInfo.bmiHeader.biSizeImage = ((((dibScaledInfo.bmiHeader.biWidth * (LONG)dibScaledInfo.bmiHeader.biBitCount) + 31) & ~31) >> 3) * dibScaledInfo.bmiHeader.biHeight;
                void* scaleMaskPtr = NULL;
                HBITMAP hBmpScaledMask = CreateDIBSection(toolbarDC, &dibScaledInfo, DIB_RGB_COLORS, &scaleMaskPtr, NULL, 0);                
                auto oldScaleMaskBmp = SelectObject(maskScaleDC, hBmpScaledMask);
                oldMaskBmp = SelectObject(maskDC, hBmpMask);
                StretchBlt(maskScaleDC, 0, 0, dibScaledInfo.bmiHeader.biWidth, dibScaledInfo.bmiHeader.biHeight,
                           maskDC, 0, 0, dibSectionInfo.bmiHeader.biWidth, dibSectionInfo.bmiHeader.biHeight, SRCCOPY);
                SelectObject(maskDC, oldMaskBmp);
                SelectObject(maskScaleDC, oldScaleMaskBmp);

                //Scale image
                void* scaleImgPtr = NULL;
                HBITMAP hbmpScaleImg = CreateDIBSection(toolbarDC, &dibScaledInfo, DIB_RGB_COLORS, &scaleImgPtr, NULL, 0);
                auto oldScaleImg = SelectObject(scaleImgDC, hbmpScaleImg);
                oldSrcBmp = SelectObject(srcDC, hBmpSrcTmp);
                StretchBlt(scaleImgDC, 0, 0, dibScaledInfo.bmiHeader.biWidth, dibScaledInfo.bmiHeader.biHeight,
                             srcDC, 0, 0, dibSectionInfo.bmiHeader.biWidth, dibSectionInfo.bmiHeader.biHeight, SRCCOPY);
                SelectObject(srcDC, oldSrcBmp);
                SelectObject(scaleImgDC, oldScaleImg);


                //Use the scaled mask to set pixels that should not be shown as magenta
                DWORD* ptrMask = reinterpret_cast<DWORD*>(scaleMaskPtr);
                DWORD * ptrImg = reinterpret_cast<DWORD*>(scaleImgPtr);
                for (int imgPos = 0; imgPos < dibScaledInfo.bmiHeader.biSizeImage; imgPos += 4)
                {
                    if ( *ptrMask != 0)
                    {
                        *ptrImg = RGB(255,0,255);
                    }

                    ++ptrMask;
                    ++ptrImg;
                }

                newImages.push_back(hbmpScaleImg);
                
                if (dibScaledInfo.bmiHeader.biWidth > newImageWidth)
                    newImageWidth = dibScaledInfo.bmiHeader.biWidth;

                if (dibScaledInfo.bmiHeader.biHeight > newImageHeight)
                    newImageHeight = dibScaledInfo.bmiHeader.biHeight;
                
               
                //tidy up
                DeleteObject(hBmpMask);
                DeleteObject(hBmpScaledMask);
                DeleteObject(hBmpSrcTmp);
                DeleteDC(srcDC);
                DeleteDC(maskDC);
                DeleteDC(maskScaleDC);
                DeleteDC(scaleImgDC);
            }

            //Create new image list
            HIMAGELIST hNewList  = ImageList_Create(newImageWidth, newImageHeight, ILC_COLOR32 | ILC_MASK, 0, 4);

            //Use the new image list
            SendMessage(toolbar->m_hWnd, TB_SETIMAGELIST, 0, (LPARAM)hNewList);

            //Set the bitmap size
            SendMessage(toolbar->m_hWnd, TB_SETBITMAPSIZE, 0, MAKELPARAM(newImageWidth, newImageHeight));

            //Add the images to the image list
            for (const auto& bmp : newImages)
            {
                ImageList_AddMasked(hNewList, bmp, RGB(255, 0, 255));
            }

            //Increase the button sizes
            int btnWidth = LOWORD(btnSize) * GetToolbarScalingFactor(toolbar->m_hWnd);
            int btnHeight = HIWORD(btnSize) * scalingFactor;
            SendMessage(toolbar->m_hWnd, TB_SETBUTTONSIZE, 0, MAKELPARAM(btnWidth, btnHeight) );
        }
    }
}