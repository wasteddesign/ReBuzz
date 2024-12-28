	if (posint >= end)
	{
#ifdef RSCODE_LOOP
		posint -= end;
		posint += params.LoopBegin;
		if (posint >= end)
			posint = params.LoopBegin;	// FIXME (doesn't work for very small loops)

#else
		state.Active = false;
		break;
#endif

	}

 