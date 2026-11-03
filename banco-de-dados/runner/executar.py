"""Executa os scripts dos roteiros no SQL Server e grava as evidências em JSON.

Cada script é dividido em passos (marcador "-- @passo id | descrição [S+n]") e lotes (GO).
Passos consecutivos com sessão marcada ([A], [B+2], [M+4]) rodam ao mesmo tempo, cada um em
sua própria conexão, como duas janelas do SSMS. A senha vem da variável de ambiente SA_SENHA
e nunca é gravada nem exibida.
"""
import datetime as dt
import decimal
import json
import os
import re
import sys
import threading
import time

import pyodbc

CONEXAO = ('DRIVER={ODBC Driver 18 for SQL Server};SERVER=localhost,1433;UID=sa;'
           'PWD=' + os.environ['SA_SENHA'] + ';TrustServerCertificate=yes;Encrypt=yes')
RE_PASSO = re.compile(r'^-- @passo (\S+) \| (.*?)(?:\s*\[([A-Z])(?:\+(\d+))?\])?\s*$')
RE_GO = re.compile(r'^\s*GO\s*;?\s*$', re.I)
RE_PREFIXO = re.compile(r'^\s*(\[[^\]]*\])+')


def limpar(texto):
    texto = RE_PREFIXO.sub('', texto).strip()
    return re.sub(r'\s*\((\d+)\)\s*\(SQL\w+\)\s*$', r' (\1)', texto)


def valor(v):
    if v is None: return None
    if isinstance(v, decimal.Decimal): return str(v)
    if isinstance(v, (dt.datetime, dt.date, dt.time)): return v.isoformat(sep=' ') if isinstance(v, dt.datetime) else v.isoformat()
    if isinstance(v, bytes): return '0x' + v.hex().upper()
    return v


def ler_passos(caminho):
    passos, atual = [], None
    for linha in open(caminho, encoding='utf-8').read().split('\n'):
        m = RE_PASSO.match(linha)
        if m:
            atual = {'id': m[1], 'descricao': m[2], 'sessao': m[3], 'atraso': int(m[4] or 0), 'linhas': []}
            passos.append(atual)
        elif atual is not None:
            atual['linhas'].append(linha)
    for p in passos:
        lotes, buf = [], []
        for l in p.pop('linhas'):
            if RE_GO.match(l):
                if ''.join(buf).strip(): lotes.append('\n'.join(buf).strip('\n'))
                buf = []
            else:
                buf.append(l)
        if ''.join(buf).strip(): lotes.append('\n'.join(buf).strip('\n'))
        p['lotes'] = lotes
    return passos


def conectar():
    c = pyodbc.connect(CONEXAO, autocommit=True)
    c.execute('SET LANGUAGE Brazilian;')
    return c


def msgs(cur, destino):
    for _, texto in (cur.messages or []):
        t = limpar(texto)
        if t and 'O idioma foi alterado' not in t and 'Changed language setting' not in t:
            destino.append(t)


def executar_lote(con, sql):
    cur = con.cursor()
    r = {'sql': sql, 'resultados': [], 'mensagens': [], 'erro': None,
         'inicio': dt.datetime.now().strftime('%H:%M:%S.%f')[:-3]}
    t0 = time.perf_counter()
    try:
        cur.execute(sql)
        while True:
            msgs(cur, r['mensagens'])
            if cur.description:
                cols = [d[0] for d in cur.description]
                linhas = [[valor(v) for v in row] for row in cur.fetchall()]
                msgs(cur, r['mensagens'])
                if len(cols) == 1 and 'Showplan' in cols[0]:
                    r.setdefault('planos', []).append(linhas[0][0] if linhas else None)
                else:
                    r['resultados'].append({'colunas': cols, 'linhas': linhas})
            if not cur.nextset():
                break
        msgs(cur, r['mensagens'])
    except pyodbc.Error as e:
        msgs(cur, r['mensagens'])
        r['erro'] = limpar(e.args[1] if len(e.args) > 1 else str(e))
    r['ms'] = round((time.perf_counter() - t0) * 1000)
    cur.close()
    return r


def executar_passo(p, con=None):
    proprio = con is None
    if proprio:
        con = conectar()
    p['spid'] = con.execute('SELECT @@SPID').fetchone()[0]
    p['resultado'] = [executar_lote(con, l) for l in p['lotes']]
    if proprio:
        con.close()


def main(scripts, saida_dir):
    os.makedirs(saida_dir, exist_ok=True)
    for _ in range(60):
        try:
            con = conectar(); break
        except pyodbc.Error:
            time.sleep(2)
    versao = con.execute('SELECT @@VERSION').fetchone()[0]
    json.dump({'versao': versao}, open(os.path.join(saida_dir, 'versao.json'), 'w'), ensure_ascii=False)
    for caminho in scripts:
        passos = ler_passos(caminho)
        i = 0
        while i < len(passos):
            if passos[i]['sessao']:
                grupo = []
                while i < len(passos) and passos[i]['sessao']:
                    grupo.append(passos[i]); i += 1
                ths = []
                for p in grupo:
                    def alvo(p=p):
                        time.sleep(p['atraso'])
                        executar_passo(p)
                    ths.append(threading.Thread(target=alvo)); ths[-1].start()
                for t in ths: t.join()
            else:
                executar_passo(passos[i], con); i += 1
        for p in passos:
            for l in p['resultado']:
                status = 'ERRO ' + l['erro'] if l['erro'] else 'ok'
                print(f"[{p['id']}] lote {l['ms']} ms -> {status}")
        nome = os.path.splitext(os.path.basename(caminho))[0]
        json.dump(passos, open(os.path.join(saida_dir, nome + '.json'), 'w', encoding='utf-8'),
                  ensure_ascii=False, indent=1)
    con.close()


if __name__ == '__main__':
    main(sys.argv[2:], sys.argv[1])
