# Propuesta de aplicación de gestión para barbería

**Aplicación de escritorio para Windows — Uso interno**

---

## ¿Qué es esta aplicación?

Una herramienta instalada en tu ordenador para gestionar el día a día de la barbería: agenda de citas, clientes, ventas y caja. Todo en un solo lugar, pensado para que sea sencillo de usar sin necesidad de conocimientos informáticos.

Está pensada para una barberia con una o varias trabajadoras.

Funciona sin Internet. Los datos se guardan en tu propio ordenador.

---

## Lo que podrás hacer

### Pantalla de inicio

Cada vez que abras la aplicación verás de un vistazo:

- Las citas del día (quién viene, a qué hora y para qué servicio)
- El dinero cobrado durante el día
- Las entradas y salidas de caja
- El balance del día (ingresos − gastos)

Y desde esta misma pantalla, con un solo clic:

- `+ Nueva cita`
- `+ Nueva venta`
- `+ Nuevo cliente`
- `+ Entrada de dinero`
- `+ Salida de dinero`

---

### Agenda

**Vista semanal.** La agenda te muestra la semana entera de un vistazo, con una columna por día y las citas de cada día dentro. Puedes moverte a la semana anterior o siguiente con un botón, y volver a la semana actual cuando quieras. Al hacer clic en un día ves su detalle completo.

- Ver toda la semana, o el detalle de un día concreto
- Crear una cita nueva (cliente, servicio, hora, notas)
- Modificar o cancelar una cita
- Marcar una cita como **realizada**, **cancelada** o **no asistida**
- Ver los datos del cliente asociado a la cita

Los estados posibles de una cita son:

| Estado | Significado |
|---|---|
| Pendiente | La cita está programada |
| Realizada | El cliente vino y se realizó el servicio |
| Cancelada | Se canceló con antelación |
| No asistida | El cliente no se presentó sin avisar |

Las citas canceladas y no asistidas no generan ninguna venta ni movimiento de dinero. Se registran por separado para que puedas ver cuántas veces ha pasado con cada cliente.

Cada cita se puede registrar con un **cliente registrado**, o con un **cliente no registrado** indicando solo su nombre (y teléfono si se quiere). Si la cita es con un cliente no registrado, la aplicación avisa amablemente ofreciendo darlo de alta.

Cuando el cliente está registrado, la cita queda vinculada a su ficha. Desde la ficha del cliente podrás ver todas sus citas pasadas y su estado, y desde la cita podrás acceder directamente a la ficha del cliente.

Cada cita tiene: fecha, hora, duración, cliente, servicio *(opcional)*, trabajadora *(opcional)*, estado y observaciones.

**El servicio es opcional al crear la cita.** Si lo indicas, aparecerá ya precargado cuando marques la cita como realizada y pases a cobrar (y podrás cambiarlo). Si no lo sabes de antemano, lo apuntas directamente en el momento de registrar la venta.

**Duración y avisos de solapamiento:** cada servicio puede tener una duración (por ejemplo, corte = 30 min). Si intentas crear una cita que se solapa con otra sin haber suficiente personal disponible en ese momento, la aplicación te avisa — pero no te impide crearla, por si de verdad hace falta.

**Horario de la barbería:** en la configuración defines tu horario habitual y los días que cierras (festivos, vacaciones). La agenda te ofrece por defecto solo esas horas, aunque siempre puedes crear una cita fuera de horario si hace falta.

---

### Clientes

- Crear, editar y consultar fichas de clientes
- Buscar un cliente por nombre o teléfono
- Eliminar o "dormir" un cliente (ver más abajo)

**Datos que se guardan de cada cliente:**

- Nombre *(obligatorio)*
- Móvil *(obligatorio)*
- Correo electrónico *(opcional)*
- Observaciones *(opcional)*
- Fecha de nacimiento *(opcional)*

Todos los datos se podrán ampliar y modificar más adelante.

**Aviso de cumpleaños:** si guardas la fecha de nacimiento de un cliente, la pantalla de inicio te avisará discretamente el día de su cumpleaños.

**Aviso de cliente no registrado:** siempre que hagas una venta o una cita con alguien que no está registrado como cliente, la aplicación te avisará de forma amable y te preguntará si quieres registrarlo. Puedes hacerlo en el momento o seguir sin registrarlo. Si el aviso te resulta molesto, puedes desactivarlo desde la configuración.

**Importante:** las ventas hechas a alguien sin registrar cuentan en los totales del día y del período, pero no generan historial personal. Si más adelante registras a esa persona, las operaciones anteriores no se recuperan.

---

### Eliminar o "dormir" un cliente

Hay dos opciones distintas:

| Opción | Qué hace | ¿Se puede deshacer? |
|---|---|---|
| **Dormir** | El cliente deja de aparecer en las búsquedas y al crear citas o ventas, pero se conserva su historial y sigue contando en las estadísticas | Sí, puedes despertarlo |
| **Eliminar** | Borra la ficha y **todo su historial** de citas y ventas | No |

Ambas piden confirmación antes de ejecutarse.

---

### Ficha del cliente

Cada cliente tiene su propia ficha con un resumen visual:

| Dato | Ejemplo |
|---|---|
| Última visita | 28/08/2026 |
| Total de visitas realizadas | 24 |
| Citas canceladas | 1 |
| Citas no asistidas | 2 |
| Total gastado | 540 € |
| Gasto medio por visita | 22,50 € |
| Frecuencia aproximada | Cada 21 días |
| Cumpleaños | 15 de marzo *(si se indicó)* |

Y desde la misma ficha, el historial completo: todas las citas, servicios realizados y productos comprados, ordenados por fecha.

---

### Trabajadoras

Si en la barbería trabaja más de una persona, puedes darlas de alta con su nombre y su horario semanal.

- Cada trabajadora tiene un horario fijo (por ejemplo, lunes a viernes de 9 a 14 y de 16 a 20)
- Puedes marcarla como **inactiva** temporalmente (vacaciones, baja) sin borrar sus datos ni su historial
- Al crear una cita o una venta, puedes asignarla a una trabajadora concreta, pero **no es obligatorio**

**Aviso de solapamiento:** si en un momento dado solo tienes una trabajadora disponible y ya hay una cita en esa franja, la aplicación te avisa al intentar crear otra. Si tienes varias trabajadoras, el aviso solo aparece cuando ya no queda ninguna libre en ese horario.

---

### Ventas

Una venta puede registrarse de dos maneras:

**1. Venta independiente** — desde el botón `+ Nueva venta` de cualquier pantalla. No está ligada a ninguna cita.

**2. Venta desde una cita** — desde la lista de citas del día, al marcar una cita como realizada se abre directamente el formulario de venta con el cliente ya precargado, y el servicio también si la cita lo tenía indicado. Todo se puede revisar y modificar antes de cobrar.

También puedes asociar manualmente una cita existente a una venta creada de forma independiente.

Esto evita introducir los mismos datos dos veces y reduce el tiempo necesario para cerrar una visita.

---

**Desde la lista de citas del día**, cada cita tiene acciones directas:

| Acción | Resultado |
|---|---|
| **Realizada** | Abre el formulario de venta con el cliente precargado (y el servicio, si la cita lo tenía) |
| **Cancelada** | Marca la cita como cancelada |
| **No asistida** | Marca la cita como no asistida |

---

**El formulario de venta** permite:

- Cliente registrado (búsqueda libre), o cliente no registrado (solo nombre, teléfono opcional) — con aviso amable para registrarlo
- Trabajadora que atiende la venta *(opcional)*
- Servicio/s del catálogo (precargado si la cita lo tenía indicado)
- Producto/s del catálogo
- Líneas personalizadas: descripción libre y precio a mano, para servicios puntuales o precios acordados en el momento
- Forma de pago (la lista la defines tú en la configuración: efectivo, tarjeta, Bizum...)
- Observaciones

**Todo se puede personalizar en cada venta.** Aunque los servicios y productos tengan un precio guardado, siempre podrás cambiar la descripción, el precio o la cantidad en esa venta concreta. Así puedes hacer un descuento, un favor o un precio especial sin tener que modificar tu lista de precios.

**Datos que se guardan de cada venta:**

- Fecha y hora
- Cliente: registrado o no registrado (nombre + teléfono opcional)
- Trabajadora (si se indica)
- Servicios y/o productos
- Líneas personalizadas (descripción libre + precio)
- Cantidad y precio de cada concepto
- Base, IVA y total
- Forma de pago
- Cita asociada (si aplica)
- Observaciones

---

### Corregir o anular una venta

Si te equivocas al registrar una venta, puedes **editarla** en cualquier momento: cambiar el cliente, los conceptos, la forma de pago o la trabajadora.

Si una venta ya no debe contar (por ejemplo, se hizo por error), se **anula** (pidiendo confirmación antes). Una venta anulada no se borra — queda visible en el historial marcada como anulada, pero deja de contar en el balance y en el IVA. Así siempre puedes ver qué pasó, sin perder la cuenta de tus ingresos reales.

**Detalle:** al confirmar una venta, la aplicación reproduce un sonido corto de confirmación (puedes desactivarlo desde la configuración).

---

### IVA

Los precios incluyen el IVA por defecto, tal como los ves y los cobras. La aplicación calcula automáticamente qué parte de lo recaudado corresponde a IVA y qué parte es base.

Desde la configuración puedes ajustar:

- El porcentaje de IVA
- Si los precios que introduces incluyen o no el IVA
- Un IVA distinto para un servicio o producto concreto, si hace falta

En el balance de caja verás el desglose: base, IVA y total del período.

---

### Servicios y productos

**Servicios** son los tratamientos que se realizan en la barbería. Tienen un precio fijo y, opcionalmente, una duración (por ejemplo, 30 minutos) que se usa para calcular el hueco que ocupan en la agenda. Aparecen como opción al crear una cita o una venta.

Ejemplos: corte de pelo, barba, corte + barba, arreglo de cejas, tratamiento capilar.

**Productos** son artículos físicos que se venden en el local. También tienen precio y se pueden añadir a cualquier venta.

Ejemplos: champú, cera, aceite de barba, acondicionador.

Tanto los servicios como los productos tienen nombre, precio y estado activo/inactivo. Los inactivos no aparecen al crear una venta ni una cita.

---

### Caja — Entradas y salidas

Además de las ventas, podrás registrar cualquier movimiento de dinero:

**Entradas:** aportación a caja, otros ingresos, ajustes.

**Salidas:** compra de material, gastos, retirada de dinero, otros pagamentos.

Cada movimiento incluye: fecha, tipo, importe, forma de pago, concepto y observaciones.

Así puedes diferenciar, por ejemplo, un pago en efectivo al comercial de una entrada por transferencia.

---

### Balance de caja

La aplicación calcula automáticamente:

```
Ventas + Entradas − Salidas = Balance
```

Puedes consultar el balance para cualquier período:

- Hoy
- Ayer
- Esta semana
- Este mes
- Mes anterior
- Período personalizado (fecha de inicio y fin)

En cada período verás también el desglose de IVA: base, IVA y total.

---

### Historial de ventas

Consulta todas las ventas registradas con filtros por:

- Fecha
- Cliente
- Servicio
- Producto
- Forma de pago
- Trabajadora
- Si está activa o anulada

---

### Análisis de clientes

La aplicación te da información sobre el comportamiento de tus clientes:

**Por cliente:**

- Número de visitas
- Total gastado
- Gasto medio por visita
- Fecha de la primera visita
- Fecha de la última visita
- Frecuencia aproximada de visita (p. ej. "cada 20 días")

**Cómo se calculan:** solo cuentan las visitas realizadas. Las citas canceladas y las no asistidas se excluyen del gasto medio y de la frecuencia, y se muestran como contadores aparte.

**Rankings globales:**

- Clientes con más visitas
- Clientes que más han gastado
- Clientes con mayor gasto medio
- Clientes que llevan más tiempo sin venir
- Trabajadoras: ventas atendidas e ingresos generados (si hay más de una)

---

### Un vistazo general

Además del detalle por cliente, la aplicación te muestra:

- Una **gráfica** con la evolución de tus ventas mes a mes
- El **cliente del mes**: quien más ha venido o más ha gastado durante el mes en curso, destacado automáticamente

---

### Detalle por trabajadora

Si hay más de una trabajadora, puedes consultar para cada una:

- Qué servicios ha hecho y cuántas veces cada uno
- Qué productos ha vendido y cuántas unidades
- Su actividad repartida por día de la semana, para detectar si algún día tiene mucha menos (o más) faena que otros
- **Qué porcentaje del trabajo total** ha hecho ella respecto al conjunto de la barbería
- **Qué porcentaje de sus ventas** corresponde a productos (y no a servicios)

Así puedes ver, por ejemplo, si una trabajadora concreta tiene los lunes mucho más flojos que el resto de días. Estos porcentajes los calcula la aplicación automáticamente — sustituyen el recuento manual que hasta ahora tenías que hacer tú cada día.

---

### Configuración

- Datos de la barbería (nombre, dirección, teléfono)
- Horario semanal y días que cierras (festivos, vacaciones)
- Gestión de servicios y productos (añadir, editar, activar/desactivar)
- Trabajadoras y su horario
- Formas de pago disponibles (las defines tú: efectivo, tarjeta, Bizum...)
- Si quieres desglosar el IVA también en las entradas y salidas de caja
- Número de copias de seguridad a conservar
- IVA: porcentaje y si los precios lo incluyen
- Hora de la copia de seguridad automática
- Activar o desactivar el aviso de cliente no registrado
- Activar o desactivar el sonido al cobrar

---

### Exportar para tu asesoría

Cuando toque presentar el IVA trimestral, puedes exportar el listado de ventas de un período a un archivo (compatible con Excel) con todos los datos: fecha, cliente, conceptos, base, IVA, total y forma de pago.

Así puedes enviárselo directamente a tu gestoría sin tener que copiar nada a mano.

---

### Ayuda integrada

La aplicación incluye un botón de ayuda `?` visible en todo momento. Al pulsarlo se abre una sección con preguntas frecuentes redactadas en lenguaje sencillo, sin tecnicismos.

**Ejemplos de preguntas incluidas:**

| Pregunta | Sección |
|---|---|
| ¿Cómo registro una venta? | Ventas |
| ¿Cómo creo una cita nueva? | Agenda |
| ¿Cómo añado un cliente? | Clientes |
| ¿Cómo marco una cita como realizada? | Agenda |
| ¿Cómo añado o modifico un servicio? | Configuración |
| ¿Cómo añado o modifico un producto? | Configuración |
| ¿Cómo registro una entrada o salida de dinero? | Caja |
| ¿Cómo consulto el historial de un cliente? | Clientes |
| ¿Cómo hago una copia de seguridad? | Copia de seguridad |
| ¿Cómo recupero una copia anterior? | Copia de seguridad |
| ¿Qué significa "no asistida"? | Agenda |
| ¿Cómo añado un concepto personalizado a una venta? | Ventas |
| ¿Cómo edito o anulo una venta? | Ventas |
| ¿Cómo añado una trabajadora? | Configuración |
| ¿Cómo exporto las ventas para mi asesoría? | Ventas |

Cada respuesta explica directamente qué hacer, paso a paso, sin usar conceptos técnicos.

---

### Copia de seguridad

La aplicación hace una copia de seguridad automática cada día a las 20:00 h (configurable). Por defecto se conservan las últimas 15 copias (también configurable), por si necesitas recuperar datos de días anteriores.

También puedes hacer una copia manual en cualquier momento con el botón `Hacer copia ahora`.

Si necesitas recuperar los datos, podrás restaurar cualquiera de las copias disponibles.

---

## Lo que NO incluye esta aplicación

Para mantenerla simple y fiable, quedan fuera del alcance:

- Reservas de citas por Internet
- Portal web para clientes
- Aplicación móvil
- Notificaciones automáticas a clientes
- Pagos online
- Integración con calendarios externos
- Conexión a la nube
- Permisos o accesos distintos entre trabajadoras (todas ven y hacen lo mismo)
- Fichaje de entrada/salida laboral o gestión de nóminas

---

## Requisitos técnicos

- **Sistema operativo:** Windows 10 o Windows 11
- **Instalación:** sencilla, sin conocimientos técnicos
- **Datos:** guardados localmente en tu ordenador


