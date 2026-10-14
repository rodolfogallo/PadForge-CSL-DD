# PadForge 5.0.0.1 — correção CSL DD

Cópia privada independente de [hifihedgehog/PadForge](https://github.com/hifihedgehog/PadForge), baseada na tag `v5.0.0`, commit `9d1cac181e1d21441a084d0eb82bc7846262771d`. A tag local `upstream-v5.0.0` preserva o snapshot importado. O histórico anterior à importação permanece no projeto original. A licença e a atribuição originais foram preservadas.

## O que foi corrigido

- Para `VID_0EB7/PID_0020`, a saída é resolvida pelo mesmo pai PnP da entrada, pelos atributos reais e pelas capacidades HID. Coleções de joystick com saída proprietária `FF00/0002`, 7 valores de 8 bits e relatório total de 8 bytes são elegíveis. O Report ID é validado em memória pelo Windows. A coleção primária de ID `01` tem preferência; se houver apenas outra coleção elegível, usa-se seu ID real. Coleções ambíguas e o protocolo `FF/64 bytes` são recusados.
- A coleção usada para ler entradas pode ser COL02 e a saída ser COL01 do mesmo volante. Outros PIDs Fanatec mantêm a seleção de transporte anterior; os pedais não são redirecionados.
- O cache de FFB só marca uma assinatura como entregue quando todas as escritas necessárias retornam sucesso. Falhas são tentadas novamente após 250 ms, mesmo com força idêntica. Um novo pedido de parada pode antecipar a tentativa, sem criar repetição ilimitada.
- Configuração de range e autocentragem precede a força Fanatec; reabertura/falha de saída invalida o estado aplicado. Uma parada limpa os cinco slots de efeito. Ao trocar de tipo de condição, a anterior é desativada.
- A escrita exige conclusão e quantidade completa de bytes. Os diagnósticos incluem caminho, ID, tamanho, fase, erro Win32, quantidade transferida, duração e motivo de omissão, limitados a uma linha por caminho/fase a cada cinco segundos.
- O modo `--fanatec-hid-probe` termina antes de iniciar o motor de entrada, o atualizador, HidHide ou HIDMaestro. Abre handles somente para metadados, com acesso zero. Não envia relatórios.

Sucesso da escrita no Windows não comprova aceitação física do comando pelo firmware. A validação de torque continua pendente do proprietário do volante.

## Validar na sua máquina

1. Extraia o pacote em uma pasta nova. Não substitua `D:\GAMES\PadForge` nesta etapa e não copie seu XML antigo para começar. Feche a instância original pela opção Exit/Sair da bandeja antes de abrir esta cópia.
2. Opcionalmente, execute `Inspecionar-HID.ps1` com PowerShell, ou inicie `PadForge.exe --fanatec-hid-probe "C:\caminho\fanatec-hid-probe.json"`. O executável conserva a solicitação de administrador do projeto original. Este modo só produz o JSON e termina. No CSL DD investigado, espera-se uma saída resolvida com ReportId `1` e ReportLength `8` no caminho COL01.
3. Para validar FFB, abra normalmente o `PadForge.exe` desta pasta e habilite a gravação de diagnósticos em Settings/Diagnostics. O arquivo `diagnostics.log` ficará ao lado dele.
4. Crie o controle virtual pretendido e associe **uma** das entradas FANATEC Wheel ao slot — prefira COL01 para começar. Só aparecer na lista de Devices não basta. A versão investigada tinha zero associações Fanatec salvas. A associação normal já pode aplicar range e autocentragem, portanto pertence à etapa de validação física.
5. Comece com intensidade geral baixa e autocentragem em zero. Teste uma força/efeito por vez; confirme também que o efeito cessa ao encerrar o teste. Não associe simultaneamente COL01 e COL02 do mesmo volante para este primeiro ensaio.
6. Se não funcionar, preserve `diagnostics.log` e o JSON do probe, informando qual entrada e slot foram usados. Procure linhas `[FanatecHidEndpoint]`, `[RawHidOutput]` e `[WheelFFB]`. Elas distinguem resolução, abertura, escrita, erro e repetição omitida.

Esta cópia conserva o atualizador do upstream. Mantenha a instalação automática desativada e não instale uma versão oficial sobre esta cópia durante a validação, pois isso substituiria a correção privada. Configurações desta execução são gravadas na pasta nova; não foi incluído XML pessoal no pacote ou no GitHub.

## Compilar e testar

Windows x64 com SDK .NET 10:

```powershell
dotnet test PadForge.Tests/PadForge.Tests.csproj -c Release --filter 'FullyQualifiedName~FanatecCslDdOutputTests|FullyQualifiedName~WheelFfbWriteCacheTests|FullyQualifiedName~AuditJuly26RoundTwentyThreeTests|FullyQualifiedName~EngineStopRumbleTests'
dotnet publish PadForge.App/PadForge.App.csproj -c Release -r win-x64 -o artifacts/csl-dd-win-x64
```

Os testes selecionados não acionam motores. Um deles consulta as capacidades do CSL DD, se conectado, sem abrir acesso de escrita. A primeira compilação também compacta os recursos gráficos e pode demorar vários minutos. A saída é portátil e inclui o runtime .NET, sem instalação separada.

## Limites

Não foram alterados firmware, drivers, parâmetros Fanatec ou a instalação original. O aplicativo completo não foi aberto pelo agente, nem o FFB foi testado fisicamente. A validação disponível cobre compilação, regressões de seleção/cache/formato e descoberta de metadados do dispositivo conectado.
