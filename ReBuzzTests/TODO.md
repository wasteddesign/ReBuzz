# Scenarios not covered

* backup files (created every 10s?)
* Other command types
* Error cases
	* When machine type in the DLL is invalid (e.g. 96)
	* When a machine fails to load (state should not be corrupted)
	* When loading a machine without any parameters defined, expected an exception
* Native machines
	* Effects (mono/mono to stereo)
	* Mono Generators (Work)
	* Multi-IO generators (MultiWork)
	* Stereo Generators as Stereo Effects (specific flag and Work())
	* Wrappers for other instruments
	* combinations of native and managed machines (e.g. a native effect with a managed generator or native->managed->native etc.)
