#pragma once

class CXMemDC
{
public:
	static void Init();
	static bool UsingBufferedPaint();

public:
	CXMemDC(CDC& dc, CWnd* pWnd);
	CXMemDC(CDC& dc, const CRect& rect);

	virtual ~CXMemDC();

	CDC& GetDC() { return m_bMemDC ? m_dcMem : m_dc; }
	BOOL IsMemDC() const { return m_bMemDC; }
	BOOL IsVistaDC() const { return m_hBufferedPaint != NULL; }


protected:
	CDC&     m_dc;
	BOOL     m_bMemDC;
	HANDLE   m_hBufferedPaint;
	CDC      m_dcMem;
	CBitmap  m_bmp;
	CBitmap* m_pOldBmp;
	CRect    m_rect;
	bool mode;

};
