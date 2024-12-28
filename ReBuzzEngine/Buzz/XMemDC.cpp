#include "stdafx.h"
#include "XMemDC.h"


typedef HPAINTBUFFER(__stdcall* BEGINBUFFEREDPAINT)(HDC, const RECT*, BP_BUFFERFORMAT, BP_PAINTPARAMS*, HDC*);
typedef HRESULT(__stdcall* ENDBUFFEREDPAINT)(HPAINTBUFFER, BOOL); bool m_bUseMemoryDC = true;
BEGINBUFFEREDPAINT m_pfBeginBufferedPaint = NULL;
ENDBUFFEREDPAINT m_pfEndBufferedPaint = NULL;

void CXMemDC::Init()
{
	HINSTANCE h = LoadLibrary(_T("UxTheme.dll"));
	if (h != NULL)
	{
		m_pfBeginBufferedPaint = (BEGINBUFFEREDPAINT)::GetProcAddress(h, "BeginBufferedPaint");
		m_pfEndBufferedPaint = (ENDBUFFEREDPAINT)::GetProcAddress(h, "EndBufferedPaint");
	}
}

bool CXMemDC::UsingBufferedPaint()
{
	return m_pfBeginBufferedPaint != NULL;
}

CXMemDC::CXMemDC(CDC& dc, CWnd* pWnd) :
	m_dc(dc), m_bMemDC(FALSE), m_hBufferedPaint(NULL), m_pOldBmp(NULL)
{
	ASSERT_VALID(pWnd);
	
	mode = false;

	pWnd->GetClientRect(m_rect);


	if (m_pfBeginBufferedPaint != NULL && m_pfEndBufferedPaint != NULL)
	{
		HDC hdcPaint = NULL;

		m_hBufferedPaint = (*m_pfBeginBufferedPaint)(dc.GetSafeHdc(), m_rect, BPBF_TOPDOWNDIB, NULL, &hdcPaint);

		if (m_hBufferedPaint != NULL && hdcPaint != NULL)
		{
			m_bMemDC = TRUE;
			m_dcMem.Attach(hdcPaint);
		}
	}
	else
	{
		if (m_bUseMemoryDC && m_dcMem.CreateCompatibleDC(&m_dc) && m_bmp.CreateCompatibleBitmap(&m_dc, m_rect.Width(), m_rect.Height()))
		{
			//-------------------------------------------------------------
			// Off-screen DC successfully created. Better paint to it then!
			//-------------------------------------------------------------
			m_bMemDC = TRUE;
			m_pOldBmp = m_dcMem.SelectObject(&m_bmp);


		}
	}
}

CXMemDC::CXMemDC(CDC& dc, const CRect& rect) :
	m_dc(dc), m_bMemDC(FALSE), m_hBufferedPaint(NULL), m_pOldBmp(NULL), m_rect(rect)
{
	ASSERT(!m_rect.IsRectEmpty());

	mode = true;

	if (m_pfBeginBufferedPaint != NULL && m_pfEndBufferedPaint != NULL)
	{
		HDC hdcPaint = NULL;

		m_hBufferedPaint = (*m_pfBeginBufferedPaint)(dc.GetSafeHdc(), m_rect, BPBF_TOPDOWNDIB, NULL, &hdcPaint);

		if (m_hBufferedPaint != NULL && hdcPaint != NULL)
		{
			m_bMemDC = TRUE;
			m_dcMem.Attach(hdcPaint);
		}
	}
	else
	{

		if (m_bUseMemoryDC && m_dcMem.CreateCompatibleDC(&m_dc) && m_bmp.CreateCompatibleBitmap(&m_dc, m_rect.Width(), m_rect.Height()))
		{
			CPoint p(m_rect.left, m_rect.top);
			m_dcMem.SetWindowOrg(p.x, p.y);

			//-------------------------------------------------------------
			// Off-screen DC successfully created. Better paint to it then!
			//-------------------------------------------------------------
			m_bMemDC = TRUE;
			m_pOldBmp = m_dcMem.SelectObject(&m_bmp);

		}
	}
}

CXMemDC::~CXMemDC()
{
	if (m_hBufferedPaint != NULL)
	{
		m_dcMem.Detach();
		(*m_pfEndBufferedPaint)(m_hBufferedPaint, TRUE);
	}
	else if (m_bMemDC)
	{
		
		//--------------------------------------
		// Copy the results to the on-screen DC:
		//--------------------------------------
		CRect rectClip;
		int nClipType = m_dc.GetClipBox(rectClip);

		if (nClipType != NULLREGION)
		{
			if (nClipType != SIMPLEREGION)
			{
				rectClip = m_rect;
			}


			if (mode)
			{
				m_dcMem.SetWindowOrg(0, 0);
				m_dc.BitBlt(rectClip.left, rectClip.top, rectClip.Width(), rectClip.Height(), &m_dcMem, 0, 0, SRCCOPY);
			}
			else
			{
				m_dc.BitBlt(rectClip.left, rectClip.top, rectClip.Width(), rectClip.Height(), &m_dcMem, rectClip.left, rectClip.top, SRCCOPY);
			}

			

		}

		m_dcMem.SelectObject(m_pOldBmp);
	}
}

