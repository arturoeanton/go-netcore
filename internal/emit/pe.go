package emit

const (
	imageBase        = 0x00400000
	sectionAlignment = 0x2000
	fileAlignment    = 0x200
	cliHeaderSize    = 72
)

// buildCLIHeader builds the COR20 (CLI) header. It points at the metadata and
// names the managed entry point token. The assembly is IL-only.
func buildCLIHeader(metadataRVA, metadataSize, entryToken uint32) []byte {
	var b []byte
	w := &writer{&b}
	w.u32(cliHeaderSize) // cb
	w.u16(2)             // MajorRuntimeVersion
	w.u16(5)             // MinorRuntimeVersion
	w.u32(metadataRVA)   // MetaData RVA
	w.u32(metadataSize)  // MetaData size
	w.u32(0x00000001)    // Flags = COMIMAGE_FLAGS_ILONLY
	w.u32(entryToken)    // EntryPointToken
	w.u32(0)             // Resources RVA
	w.u32(0)             // Resources size
	w.u32(0)             // StrongNameSignature RVA
	w.u32(0)             // StrongNameSignature size
	w.u32(0)             // CodeManagerTable RVA
	w.u32(0)             // CodeManagerTable size
	w.u32(0)             // VTableFixups RVA
	w.u32(0)             // VTableFixups size
	w.u32(0)             // ExportAddressTableJumps RVA
	w.u32(0)             // ExportAddressTableJumps size
	w.u32(0)             // ManagedNativeHeader RVA
	w.u32(0)             // ManagedNativeHeader size
	return b
}

// buildPE wraps the assembled .text section in a minimal managed PE32 image.
// The CLI header sits at the start of .text (so its RVA == textRVA).
func buildPE(text []byte, textRVA uint32) []byte {
	textVirtualSize := uint32(len(text))
	textRawSize := roundUp32(textVirtualSize, fileAlignment)
	sizeOfImage := roundUp32(textRVA+textVirtualSize, sectionAlignment)
	baseOfData := roundUp32(textRVA+textVirtualSize, sectionAlignment)
	const sizeOfHeaders = fileAlignment // 0x200

	var b []byte
	w := &writer{&b}

	// --- DOS header + stub (128 bytes), e_lfanew = 0x80 ---
	dos := make([]byte, 0x80)
	dos[0], dos[1] = 'M', 'Z'
	dos[0x3C] = 0x80 // e_lfanew
	w.bytes(dos)

	// --- PE signature ---
	w.bytes([]byte{'P', 'E', 0, 0})

	// --- COFF header ---
	w.u16(0x014C) // Machine = I386 (AnyCPU IL)
	w.u16(1)      // NumberOfSections
	w.u32(0)      // TimeDateStamp
	w.u32(0)      // PointerToSymbolTable
	w.u32(0)      // NumberOfSymbols
	w.u16(0x00E0) // SizeOfOptionalHeader (PE32)
	w.u16(0x2102) // Characteristics = EXECUTABLE_IMAGE | 32BIT_MACHINE | DLL

	// --- Optional header (PE32) ---
	w.u16(0x010B)      // Magic = PE32
	w.u8(0)            // MajorLinkerVersion
	w.u8(0)            // MinorLinkerVersion
	w.u32(textRawSize) // SizeOfCode
	w.u32(0)           // SizeOfInitializedData
	w.u32(0)           // SizeOfUninitializedData
	w.u32(0)           // AddressOfEntryPoint (unused for managed .NET Core)
	w.u32(textRVA)     // BaseOfCode
	w.u32(baseOfData)  // BaseOfData (PE32 only)
	w.u32(imageBase)   // ImageBase
	w.u32(sectionAlignment)
	w.u32(fileAlignment)
	w.u16(4)             // MajorOperatingSystemVersion
	w.u16(0)             // MinorOperatingSystemVersion
	w.u16(0)             // MajorImageVersion
	w.u16(0)             // MinorImageVersion
	w.u16(4)             // MajorSubsystemVersion
	w.u16(0)             // MinorSubsystemVersion
	w.u32(0)             // Win32VersionValue
	w.u32(sizeOfImage)   // SizeOfImage
	w.u32(sizeOfHeaders) // SizeOfHeaders
	w.u32(0)             // CheckSum
	w.u16(3)             // Subsystem = WINDOWS_CUI (console)
	w.u16(0x8560)        // DllCharacteristics
	w.u32(0x00100000)    // SizeOfStackReserve
	w.u32(0x00001000)    // SizeOfStackCommit
	w.u32(0x00100000)    // SizeOfHeapReserve
	w.u32(0x00001000)    // SizeOfHeapCommit
	w.u32(0)             // LoaderFlags
	w.u32(16)            // NumberOfRvaAndSizes

	// Data directories (16). Only [14] CLR runtime header is set.
	for i := 0; i < 16; i++ {
		if i == 14 {
			w.u32(textRVA)       // CLI header RVA (start of .text)
			w.u32(cliHeaderSize) // CLI header size
		} else {
			w.u32(0)
			w.u32(0)
		}
	}

	// --- Section header: .text ---
	name := make([]byte, 8)
	copy(name, ".text")
	w.bytes(name)
	w.u32(textVirtualSize) // VirtualSize
	w.u32(textRVA)         // VirtualAddress
	w.u32(textRawSize)     // SizeOfRawData
	w.u32(sizeOfHeaders)   // PointerToRawData
	w.u32(0)               // PointerToRelocations
	w.u32(0)               // PointerToLinenumbers
	w.u16(0)               // NumberOfRelocations
	w.u16(0)               // NumberOfLinenumbers
	w.u32(0x60000020)      // Characteristics = CNT_CODE | MEM_EXECUTE | MEM_READ

	// Pad headers to SizeOfHeaders.
	for len(b) < sizeOfHeaders {
		w.u8(0)
	}

	// --- .text raw data ---
	w.bytes(text)
	for len(b) < int(sizeOfHeaders+textRawSize) {
		w.u8(0)
	}

	return b
}

func roundUp32(n, a uint32) uint32 { return (n + a - 1) / a * a }
