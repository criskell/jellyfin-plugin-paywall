# Jellyfin Paywall

Plugin que só libera a biblioteca do Jellyfin para quem pagou. Aceita pagamento único ou
assinatura, e não é casado com nenhum meio de pagamento.

## Como está organizado

    src/Paywall.Domain          regras de acesso: plano, concessão, pedido. Sem dependência nenhuma.
    src/Paywall.Application     casos de uso e portas (pagamento, persistência, bloqueio).
    src/Paywall.Infrastructure  SQLite próprio do plugin e adaptadores de pagamento.
    src/Jellyfin.Plugin.Paywall raiz de composição: plugin, endpoints, tarefa agendada.

A direção das dependências aponta para dentro: trocar de PSP, de banco ou até de servidor de
mídia é escrever adaptador, não mexer em regra.

## Trocar o meio de pagamento

Implemente `IPaymentProvider` e registre em `PaywallServiceRegistrator`. A interface pede
duas coisas: abrir uma cobrança e traduzir o webhook. Quem sabe assinar recorrência implementa
também `ISupportsSubscriptionCancellation`.

Dois provedores já vêm prontos:

`manual-pix` gera o copia e cola no padrão BR Code direto da sua chave, sem intermediário.
Não tem como saber que o dinheiro entrou, então a liberação é feita pelo administrador.

`asaas` faz cobrança avulsa e assinatura Pix recorrente, com confirmação automática por
webhook. Precisa de chave de API e de um token de webhook — sem o token o provedor nem
aparece, porque qualquer um poderia forjar um pagamento aprovado. O Asaas exige CPF ou CNPJ
do pagador.

A cobrança que a recorrência gera sozinha todo mês chega sem pedido aberto por aqui. Nesse
caso o assinante é encontrado pelo id da recorrência e um pedido de renovação é criado.

Em cripto há três: `btcpay` para instância própria de BTCPay Server, `opennode` para Bitcoin
e Lightning hospedados, e `nowpayments` para centenas de moedas com página de checkout.
Nenhum deles atende assinatura, porque cripto não tem débito automático: ninguém consegue
puxar o pagamento do usuário todo mês. Em cripto, "mensal" é um plano de pagamento único de
30 dias que o usuário renova pagando de novo, e o prazo restante não se perde.

Cada um assina o webhook do seu jeito e isso fica no adaptador: BTCPay usa HMAC-SHA256 sobre
o corpo cru, a OpenNode assina só o id da cobrança com a própria chave de API e notifica em
formulário codificado, e a NOWPayments usa HMAC-SHA512 sobre o JSON com as chaves em ordem
alfabética.

## Como o bloqueio funciona

O núcleo decide liberar ou negar; `JellyfinAccessEnforcer` aplica via `UpdatePolicyAsync`, o
mesmo caminho do painel. Dois modos: desabilitar a conta, que derruba a sessão na hora, ou
esconder as bibliotecas pagas e deixar só as gratuitas.

O acesso é reavaliado quando a conta é criada, a cada início de sessão e de hora em hora pela
tarefa agendada. Só a tarefa deixaria uma conta nova, ou um plano recém-vencido, liberados até
a próxima passagem.

## Banco

O plugin abre o próprio `paywall.db`, separado do `jellyfin.db`. O schema do servidor é dele
e migra a cada release; dados de cobrança não podem ficar reféns disso.

## Portal do usuário

Quem está sem acesso entra em `/Paywall/Portal`, escolhe o plano, recebe o Pix e vê a tela
liberar sozinha quando o pagamento cai. É página própria porque plugin não injeta interface
no cliente web do Jellyfin.

## Configuração

Tudo pelo painel, em Painel > Plugins > Paywall: planos, chaves, tolerância e modo de bloqueio.
A página também mostra a URL exata para cadastrar no webhook do Asaas e lista os assinantes com
a situação de cada um, que é por onde se confirma um Pix manual.

## Build

    ./build.sh

Copie `artifacts/Paywall_1.0.0.0/` para a pasta `plugins/` do Jellyfin e reinicie.
