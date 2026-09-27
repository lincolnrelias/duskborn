# Otimização de CPU e Hordas de Inimigos

Nas Noites 5, 6 e 7 do Duskborn, dezenas ou centenas de inimigos convergem simultaneamente para os jogadores. Sem otimização cuidadosa, o loop principal de atualização consome os ciclos da CPU, gerando quedas bruscas de taxa de quadros (framerate drops).

---

## 1. Escalonamento de Busca de Alvos (Staggered Target Acquisition)

Calcular `PlayerRegistry.FindNearest(transform.position)` dentro do `Update()` a cada quadro resulta em complexidade $O(E \times P \times \text{FPS})$.

### Padrão Recomendado:
Adicione um intervalo com jitter temporal (para evitar picos no mesmo frame):

```csharp
private float _targetScanTimer;
private const float ScanIntervalMin = 0.25f;
private const float ScanIntervalMax = 0.40f;

protected virtual void Update()
{
    // ...
    _targetScanTimer -= Time.deltaTime;
    if (_targetScanTimer <= 0f)
    {
        _targetScanTimer = UnityEngine.Random.Range(ScanIntervalMin, ScanIntervalMax);
        AcquireTarget();
    }
}
```

---

## 2. Controle de Frequência do NavMeshAgent

Chamar `Agent.SetDestination(target.position)` a cada quadro força o recálculo frequente de caminhos pelo sistema de navegação da Unity:

- Verifique a distância em relação ao destino anterior antes de chamar `SetDestination`:
  ```csharp
  if (Vector3.SqrMagnitude(CurrentTarget.position - _lastDest) > 1.5f * 1.5f)
  {
      _lastDest = CurrentTarget.position;
      Agent.SetDestination(_lastDest);
  }
  ```
- Para distâncias curtas ou inimigos já em alcance de ataque (`attackRange`), pause a navegação com `Agent.ResetPath()` ou `Agent.isStopped = true`.

---

## 3. Desativação e LOD de Animação e Física

- **Ragdolls**: Inimigos com `EnemyRagdoll` devem manter `isKinematic = true` em todos os rigidbodies e colliders desativados enquanto vivos. O modo ragdoll só deve ser ativado no momento exato do impacto fatal e desativado após o repouso.
- **Animator Culling**:
  - Configure `CullingMode = CullUpdateTransforms` no `Animator` dos inimigos para poupar processamento quando o monstro estiver fora da visão da câmera.
- **Calculo de Velocidade Local**:
  - Em vez de realizar `transform.InverseTransformDirection` e atualizações vetoriais a cada quadro para inimigos distantes, calcule apenas com base na magnitude linear da velocidade ou use LOD de tick.

---

## 4. Otimização de Chamadas de Rede (FishNet RPCs)

Evite disparar `[ObserversRpc]` individuais para pequenos efeitos sonoros e visuais a cada golpe em hordas:
- Agrupe eventos de dano em lote se o volume for extremo.
- Restrinja o alcance de transmissão de RPCs de efeitos secundários através de observadores por distância (FishNet Grid / Proximity Conditionals).
