namespace Spice86.ViewModels;

using Avalonia.Collections;
using Avalonia.Controls;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Iced.Intel;

using Spice86.Core.Emulator;
using Spice86.Core.Emulator.CPU;
using Spice86.Core.Emulator.InternalDebugger;
using Spice86.Core.Emulator.Memory;
using Spice86.Interfaces;
using Spice86.MemoryWrappers;
using Spice86.Models.Debugging;
using Spice86.Shared.Utils;

using System.ComponentModel;

public partial class DisassemblyViewModel : ViewModelBase, IInternalDebugger {
    private readonly IPauseStatus? _pauseStatus;
    private bool _needToUpdateDisassembly = true;
    private IMemory? _memory;
    private State? _state;
    private IProgramExecutor? _programExecutor;

    [ObservableProperty]
    private AvaloniaList<CpuInstructionInfo> _instructions = new();
    
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StepInstructionCommand))]
    [NotifyCanExecuteChangedFor(nameof(UpdateDisassemblyCommand))]
    private bool _isPaused;

    [ObservableProperty]
    private bool _isGdbServerAvailable;

    [ObservableProperty]
    private int _numberOfInstructionsShown = 50;

    [ObservableProperty]
    private uint? _startAddress;

    public DisassemblyViewModel() {
        if (!Design.IsDesignMode) {
            throw new InvalidOperationException("This constructor is not for runtime usage");
        }
    }

    public DisassemblyViewModel(IPauseStatus pauseStatus) {
        _pauseStatus = pauseStatus;
        IsPaused = pauseStatus.IsPaused;
        _pauseStatus.PropertyChanged += OnPauseStatusChanged;
    }

    private void OnPauseStatusChanged(object? sender, PropertyChangedEventArgs e) {
        IsPaused = _pauseStatus?.IsPaused is true;
        if (!IsPaused) {
            return;
        }
        _needToUpdateDisassembly = true;
        StartAddress ??= _state?.IpPhysicalAddress;
    }

    public void Visit<T>(T component) where T : IDebuggableComponent {
        switch (component) {
            case IMemory mem:
                _memory ??= mem;
                break;
            case State state: {
                _state ??= state;
                if (_needToUpdateDisassembly && IsPaused) {
                    UpdateDisassembly();
                }
                break;
            }
            case IProgramExecutor programExecutor:
                _programExecutor ??= programExecutor;
                IsGdbServerAvailable = programExecutor.IsGdbCommandHandlerAvailable;
                break;
        }
    }

    [RelayCommand(CanExecute = nameof(IsPaused))]
    public void StepInstruction() => _programExecutor?.StepInstruction();

    [RelayCommand(CanExecute = nameof(IsPaused))]
    public void GoToCsIp() {
        StartAddress = _state?.IpPhysicalAddress;
        _needToUpdateDisassembly = true;
        UpdateDisassemblyCommand.Execute(null);
    }
    [RelayCommand(CanExecute = nameof(IsPaused))]
    private void UpdateDisassembly() {
        if(_state is null || _memory is null || StartAddress is null) {
            return;
        }
        _needToUpdateDisassembly = false;
        CodeReader codeReader = CreateCodeReader(_memory, out EmulatedMemoryStream emulatedMemoryStream);
        Decoder decoder = InitializeDecoder(codeReader, StartAddress.Value);
        try {
            DecodeInstructions(_state, _memory, emulatedMemoryStream, decoder, StartAddress.Value);
        } finally {
            emulatedMemoryStream.Dispose();
        }
    }

    private void DecodeInstructions(State state, IMemory memory, EmulatedMemoryStream emulatedMemoryStream,
        Decoder decoder, uint startAddress) {
        int byteOffset = 0;
        emulatedMemoryStream.Position = startAddress;
        while (Instructions.Count < NumberOfInstructionsShown) {
            long instructionAddress = emulatedMemoryStream.Position;
            decoder.Decode(out Instruction instruction);
            CpuInstructionInfo instructionInfo = new() {
                Instruction = instruction,
                Address = (uint)instructionAddress,
                Length = instruction.Length,
                IP16 = instruction.IP16,
                IP32 = instruction.IP32,
                MemorySegment = instruction.MemorySegment,
                SegmentPrefix = instruction.SegmentPrefix,
                IsStackInstruction = instruction.IsStackInstruction,
                IsIPRelativeMemoryOperand = instruction.IsIPRelativeMemoryOperand,
                IPRelativeMemoryAddress = instruction.IPRelativeMemoryAddress,
                SegmentedAddress =
                    ConvertUtils.ToSegmentedAddressRepresentation(state.CS, (ushort)(state.IP + byteOffset - 10)),
                FlowControl = instruction.FlowControl,
                Bytes = $"{Convert.ToHexString(memory.GetData((uint)instructionAddress, (uint)instruction.Length))}"
            };
            if (instructionAddress == state.IpPhysicalAddress) {
                instructionInfo.IsCsIp = true;
            }
            Instructions.Add(instructionInfo);
            byteOffset += instruction.Length;
        }
    }

    private Decoder InitializeDecoder(CodeReader codeReader, uint currentIp) {
        Decoder decoder = Decoder.Create(16, codeReader, currentIp,
            DecoderOptions.Loadall286 | DecoderOptions.Loadall386);
        Instructions.Clear();
        return decoder;
    }

    private static CodeReader CreateCodeReader(IMemory memory, out EmulatedMemoryStream emulatedMemoryStream) {
        emulatedMemoryStream = new EmulatedMemoryStream(memory);
        CodeReader codeReader = new StreamCodeReader(emulatedMemoryStream);
        return codeReader;
    }
}