"""Instala cada app no emulador, executa os passos do roteiro e salva prints reais (adb screencap)."""
import os, re, subprocess, time, xml.etree.ElementTree as ET

APKS = 'android/{m}/build/outputs/apk/debug/{m}-debug.apk'
SAIDA = 'prints'
os.makedirs(SAIDA, exist_ok=True)


def adb(*args, saida=False):
    r = subprocess.run(['adb', *args], capture_output=True)
    return r.stdout if saida else r.returncode


def shell(cmd):
    return subprocess.run(['adb', 'shell', cmd], capture_output=True, text=True).stdout


def print_(nome, espera=0.8):
    time.sleep(espera)
    png = adb('exec-out', 'screencap', '-p', saida=True)
    open(f'{SAIDA}/{nome}.png', 'wb').write(png)
    print('print', nome, len(png), 'bytes', flush=True)


def arvore():
    shell('uiautomator dump /sdcard/ui.xml')
    return ET.fromstring(shell('cat /sdcard/ui.xml'))


def achar(id=None, texto=None):
    for _ in range(10):
        for n in arvore().iter('node'):
            if (id and n.get('resource-id', '').endswith(':id/' + id)) or (texto and n.get('text') == texto):
                x1, y1, x2, y2 = map(int, re.findall(r'\d+', n.get('bounds')))
                return (x1 + x2) // 2, (y1 + y2) // 2
        time.sleep(0.5)
    raise RuntimeError(f'componente não encontrado: {id or texto}')


def tocar(id=None, texto=None):
    x, y = achar(id, texto)
    shell(f'input tap {x} {y}')


def digitar(id, valor):
    tocar(id)
    time.sleep(0.3)
    shell('input text ' + valor.replace(' ', '%s'))
    time.sleep(0.3)


def fechar_teclado():
    shell('input keyevent 111')   # ESC esconde o teclado sem sair da tela
    time.sleep(0.5)


def abrir(pacote, limpar=False):
    shell(f'am force-stop {pacote}')
    if limpar:
        shell(f'pm clear {pacote}')
    shell(f'am start -W -n {pacote}/.MainActivity')
    time.sleep(1.5)


def instalar(modulo):
    rc = adb('install', '-r', APKS.format(m=modulo))
    print('instalado', modulo, rc, flush=True)


# ---------- preparação do emulador ----------
shell('input keyevent 82')                               # desbloqueia
shell('settings put system screen_off_timeout 1800000')
shell('cmd uimode night no')                             # tema claro

# ---------- Aula 1 ----------
instalar('aula1_primeiroapp')
abrir('br.unip.mobile.aula1')
print_('aula1_01_app_em_execucao')

# ---------- Aula 2 ----------
instalar('aula2_login')
abrir('br.unip.mobile.aula2')
print_('aula2_01_tela_login')
digitar('emailEditText', 'carlos.eduardo@aluno.unip.br')
digitar('senhaEditText', 'minhasenha')
fechar_teclado()
print_('aula2_02_campos_preenchidos')
tocar('entrarButton')
print_('aula2_03_botao_entrar', 0.6)

# ---------- Aula 4 ----------
instalar('aula4_menu')
abrir('br.unip.mobile.aula4')
print_('aula4_01_menu')
tocar('consultarButton')
print_('aula4_02_opcao_consultar', 0.6)
time.sleep(2.5)
tocar('configuracoesButton')
print_('aula4_03_opcao_configuracoes', 0.6)
time.sleep(2.5)
tocar('sairButton')
print_('aula4_04_confirmar_saida')
tocar(texto='CANCELAR')

# ---------- Aula 5 ----------
instalar('aula5_checkbox')
abrir('br.unip.mobile.aula5')
print_('aula5_01_tela_inicial')
tocar('verificarButton')
print_('aula5_02_nenhuma_opcao')
tocar('javaCheckBox'); tocar('flutterCheckBox'); tocar('pythonCheckBox')
tocar('verificarButton')
print_('aula5_03_tres_opcoes')
tocar('flutterCheckBox')
tocar('verificarButton')
print_('aula5_04_sem_mobile')

# ---------- Aula 6 ----------
instalar('aula6_sharedprefs')
abrir('br.unip.mobile.aula6', limpar=True)
print_('aula6_01_primeira_abertura')
digitar('nomeEditText', 'Carlos Eduardo')
fechar_teclado()
print_('aula6_02_nome_digitado')
tocar('salvarButton')
print_('aula6_03_nome_salvo', 0.6)
xml_prefs = shell('run-as br.unip.mobile.aula6 cat shared_prefs/preferencias_usuario.xml')
open(f'{SAIDA}/aula6_arquivo_preferencias.txt', 'w').write(
    '$ adb shell run-as br.unip.mobile.aula6 cat shared_prefs/preferencias_usuario.xml\n' + xml_prefs)
shell('am force-stop br.unip.mobile.aula6')              # fecha o app completamente
time.sleep(2)
shell('am start -W -n br.unip.mobile.aula6/.MainActivity')
print_('aula6_04_reaberto_nome_recuperado', 1.5)

# ---------- Aula 7 ----------
instalar('aula7_seguranca')
casos = [('', '', 'aula7_01_campos_vazios'),
         ('carlos', '123456', 'aula7_02_email_invalido'),
         ('carlos@email.com', '123', 'aula7_03_senha_curta'),
         ('carlos@email.com', 'Segura2026', 'aula7_04_login_validado')]
for email, senha, nome in casos:
    abrir('br.unip.mobile.aula7')
    if email: digitar('emailEditText', email)
    if senha: digitar('senhaEditText', senha)
    fechar_teclado()
    tocar('entrarButton')
    print_(nome)

print('fim', flush=True)
