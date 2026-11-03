"""Resume um trace Speedscope (dotnet-trace convert): métodos com mais tempo próprio e inclusivo."""
import json, sys, collections

d = json.load(open(sys.argv[1]))
frames = [f['name'] for f in d['shared']['frames']]
proprio = collections.Counter(); inclusivo = collections.Counter(); total = 0.0
for p in d['profiles']:
    if p.get('type') == 'evented':
        pilha, ultimo = [], None
        for e in p['events']:
            t = e['at']
            if ultimo is not None and pilha:
                dt = t - ultimo; total += dt
                proprio[pilha[-1]] += dt
                for f in set(pilha): inclusivo[f] += dt
            ultimo = t
            if e['type'] == 'O': pilha.append(e['frame'])
            elif pilha: pilha.pop()
    elif p.get('type') == 'sampled':
        for amostra, peso in zip(p['samples'], p['weights']):
            total += peso
            if amostra: proprio[amostra[-1]] += peso
            for f in set(amostra): inclusivo[f] += peso

def nome(i): return frames[i][:110]
print(f"tempo total amostrado (todas as threads): {total:,.0f} {d['profiles'][0].get('unit','')}")
print("\nTOP 12 – tempo próprio")
for f, v in proprio.most_common(12): print(f"{100*v/total:6.2f}%  {nome(f)}")
print("\nTOP 15 – tempo inclusivo em código HubPedidos / EF Core / SQLite")
filtro = [(f, v) for f, v in inclusivo.most_common() if any(k in frames[f] for k in ('HubPedidos', 'EntityFrameworkCore', 'Sqlite'))]
for f, v in filtro[:15]: print(f"{100*v/total:6.2f}%  {nome(f)}")
