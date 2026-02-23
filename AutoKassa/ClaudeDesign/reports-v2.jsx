import { useState, useRef, useEffect, useCallback } from "react";

/* ====== HELPERS ====== */
const fmt    = n => { const a = Math.abs(n); return (n > 0 ? "+" : n < 0 ? "−" : "") + a.toLocaleString("ru-RU"); };
const fmtAbs = n => Math.abs(n).toLocaleString("ru-RU");

/* ====== DATA ====== */
const transactions = [
  { id:1,  date:"22.02.2026", type:"income",  cat:"Ремонт авто",  amount:195663,    wallet:"cash", desc:"Замена двигателя Toyota Camry" },
  { id:2,  date:"22.02.2026", type:"expense", cat:"Аренда",       amount:-6343,     wallet:"card", desc:"Аренда помещения февраль (частичная)" },
  { id:3,  date:"22.02.2026", type:"income",  cat:"Диагностика",  amount:54745,     wallet:"card", desc:"Компьютерная диагностика BMW X5" },
  { id:4,  date:"22.02.2026", type:"income",  cat:"Диагностика",  amount:123224,    wallet:"card", desc:"Диагностика подвески Kia Sportage" },
  { id:5,  date:"22.02.2026", type:"income",  cat:"Диагностика",  amount:3456363,   wallet:"cash", desc:"Полная диагностика Mercedes E-class" },
  { id:6,  date:"22.02.2026", type:"income",  cat:"Диагностика",  amount:12345215,  wallet:"card", desc:"Диагностика электрики VW Passat" },
  { id:7,  date:"22.02.2026", type:"expense", cat:"Аренда",       amount:-12476773, wallet:"cash", desc:"Аренда помещения за квартал" },
  { id:8,  date:"19.02.2026", type:"expense", cat:"Зарплата",     amount:-55000,    wallet:"cash", desc:"Зарплата мастера Иванова" },
  { id:9,  date:"18.02.2026", type:"expense", cat:"Запчасти",     amount:-34888,    wallet:"cash", desc:"Фильтры и масло для ТО" },
  { id:10, date:"17.02.2026", type:"income",  cat:"Ремонт авто",  amount:85000,     wallet:"card", desc:"Ремонт коробки передач Honda Accord" },
  { id:11, date:"15.02.2026", type:"expense", cat:"Коммунальные", amount:-12400,    wallet:"card", desc:"Электроэнергия февраль" },
  { id:12, date:"14.02.2026", type:"income",  cat:"ТО",           amount:47000,     wallet:"cash", desc:"Плановое ТО Hyundai Solaris" },
  { id:13, date:"12.02.2026", type:"expense", cat:"Аренда",       amount:-34000,    wallet:"cash", desc:"Аренда оборудования" },
  { id:14, date:"10.02.2026", type:"income",  cat:"Диагностика",  amount:28000,     wallet:"card", desc:"Диагностика перед покупкой Ford Focus" },
  { id:15, date:"08.02.2026", type:"expense", cat:"Запчасти",     amount:-18500,    wallet:"cash", desc:"Тормозные колодки и диски" },
];

const catColors = {
  "Аренда":"#6366f1","Диагностика":"#22c55e","Ремонт авто":"#f59e0b",
  "Зарплата":"#ec4899","Запчасти":"#8b5cf6","Коммунальные":"#06b6d4",
  "ТО":"#10b981","Другое":"#94a3b8"
};

const periodBars = [
  {label:"01.02",inc:0,        exp:0},
  {label:"05.02",inc:0,        exp:0},
  {label:"08.02",inc:0,        exp:18500},
  {label:"10.02",inc:28000,    exp:0},
  {label:"12.02",inc:0,        exp:34000},
  {label:"14.02",inc:47000,    exp:0},
  {label:"15.02",inc:0,        exp:12400},
  {label:"17.02",inc:85000,    exp:0},
  {label:"18.02",inc:0,        exp:34888},
  {label:"19.02",inc:0,        exp:55000},
  {label:"22.02",inc:16175210, exp:12483116},
];

/* ====== ICONS ====== */
const IC = {
  balance:<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><line x1="12" y1="1" x2="12" y2="23"/><path d="M17 5H9.5a3.5 3.5 0 000 7h5a3.5 3.5 0 010 7H6"/></svg>,
  pie:    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="M21.21 15.89A10 10 0 118 2.83"/><path d="M22 12A10 10 0 0012 2v10z"/></svg>,
  list:   <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><line x1="8" y1="6" x2="21" y2="6"/><line x1="8" y1="12" x2="21" y2="12"/><line x1="8" y1="18" x2="21" y2="18"/><line x1="3" y1="6" x2="3.01" y2="6"/><line x1="3" y1="12" x2="3.01" y2="12"/><line x1="3" y1="18" x2="3.01" y2="18"/></svg>,
  pdf:    <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="M14 2H6a2 2 0 00-2 2v16a2 2 0 002 2h12a2 2 0 002-2V8z"/><polyline points="14 2 14 8 20 8"/><line x1="16" y1="13" x2="8" y2="13"/><line x1="16" y1="17" x2="8" y2="17"/><polyline points="10 9 9 9 8 9"/></svg>,
  excel:  <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><rect x="3" y="3" width="18" height="18" rx="2"/><line x1="9" y1="9" x2="15" y2="15"/><line x1="15" y1="9" x2="9" y2="15"/></svg>,
  up:     <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round"><line x1="12" y1="19" x2="12" y2="5"/><polyline points="5 12 12 5 19 12"/></svg>,
  dn:     <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round"><line x1="12" y1="5" x2="12" y2="19"/><polyline points="19 12 12 19 5 12"/></svg>,
  cash:   <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><rect x="1" y="4" width="22" height="16" rx="2"/><circle cx="12" cy="12" r="4"/></svg>,
  card:   <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><rect x="1" y="4" width="22" height="16" rx="2" ry="2"/><line x1="1" y1="10" x2="23" y2="10"/></svg>,
  chevD:  <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><polyline points="6 9 12 15 18 9"/></svg>,
  search: <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><circle cx="11" cy="11" r="8"/><line x1="21" y1="21" x2="16.65" y2="16.65"/></svg>,
  close:  <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round"><line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/></svg>,
  sortU:  <svg width="10" height="10" viewBox="0 0 24 24" fill="currentColor"><path d="M12 4l-8 8h16z"/></svg>,
  sortD:  <svg width="10" height="10" viewBox="0 0 24 24" fill="currentColor"><path d="M12 20l8-8H4z"/></svg>,
  drilldown: <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><polyline points="9 18 15 12 9 6"/></svg>,
  back:   <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><polyline points="15 18 9 12 15 6"/></svg>,
  note:   <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="M14 2H6a2 2 0 00-2 2v16a2 2 0 002 2h12a2 2 0 002-2V8z"/><polyline points="14 2 14 8 20 8"/></svg>,
};

/* ====== ANIMATED NUMBER ====== */
function AnimNum({ value, prefix = "", suffix = " ₽", color }) {
  const [display, setDisplay] = useState(value);
  const prev = useRef(value);
  useEffect(() => {
    if (prev.current === value) return;
    const start = prev.current, end = value, dur = 600;
    const t0 = performance.now();
    const step = ts => {
      const p = Math.min((ts - t0) / dur, 1);
      const ease = 1 - Math.pow(1 - p, 3);
      setDisplay(Math.round(start + (end - start) * ease));
      if (p < 1) requestAnimationFrame(step);
      else { prev.current = end; setDisplay(end); }
    };
    requestAnimationFrame(step);
  }, [value]);
  const abs = Math.abs(display);
  const sign = display > 0 ? "+" : display < 0 ? "−" : "";
  return (
    <span style={{ color, fontFamily: "'JetBrains Mono', monospace" }}>
      {prefix}{sign}{abs.toLocaleString("ru-RU")}{suffix}
    </span>
  );
}

/* ====== DONUT CHART ====== */
function DonutChart({ data, size = 200, thickness = 34, onCatClick }) {
  const [hovered, setHovered] = useState(null);
  const [mounted, setMounted] = useState(false);
  useEffect(() => { const t = setTimeout(() => setMounted(true), 50); return () => clearTimeout(t); }, []);

  const r = (size - thickness) / 2;
  const cx = size / 2, cy = size / 2;
  const circ = 2 * Math.PI * r;
  const total = data.reduce((s, d) => s + d.value, 0);
  let offset = 0;
  const segments = data.map((d) => {
    const pct = d.value / total;
    const dash = mounted ? pct * circ : 0;
    const gap = circ - dash;
    const seg = { ...d, dash, gap, offset: offset * circ };
    offset += pct;
    return seg;
  });

  return (
    <div style={{ position: "relative", width: size, height: size, flexShrink: 0 }}>
      <svg width={size} height={size} style={{ transform: "rotate(-90deg)" }}>
        <circle cx={cx} cy={cy} r={r} fill="none" stroke="#f0f2f5" strokeWidth={thickness} />
        {segments.map((s, i) => (
          <circle key={i} cx={cx} cy={cy} r={r} fill="none"
            stroke={s.color}
            strokeWidth={hovered === i ? thickness + 5 : thickness}
            strokeDasharray={`${s.dash} ${circ - s.dash}`}
            strokeDashoffset={-s.offset}
            style={{ transition: "stroke-width .2s, opacity .2s, stroke-dasharray .8s ease", opacity: hovered !== null && hovered !== i ? 0.3 : 1, cursor: "pointer" }}
            onMouseEnter={() => setHovered(i)}
            onMouseLeave={() => setHovered(null)}
            onClick={() => onCatClick && onCatClick(s.label)}
          />
        ))}
      </svg>
      <div style={{ position:"absolute", inset:0, display:"flex", flexDirection:"column", alignItems:"center", justifyContent:"center", textAlign:"center", pointerEvents:"none" }}>
        {hovered !== null ? (
          <div style={{ animation: "fadeIn .15s ease" }}>
            <div style={{ fontSize: 10, color: "#667085", fontWeight: 600, textTransform: "uppercase", letterSpacing: ".5px" }}>{segments[hovered].label}</div>
            <div style={{ fontSize: 16, fontWeight: 700, color: segments[hovered].color, fontFamily: "'JetBrains Mono', monospace", marginTop: 2 }}>{fmtAbs(segments[hovered].value)}</div>
            <div style={{ fontSize: 10, color: "#98a2b3" }}>{((segments[hovered].value / total) * 100).toFixed(1)}%</div>
          </div>
        ) : (
          <>
            <div style={{ fontSize: 10, color: "#98a2b3", fontWeight: 500 }}>ВСЕГО</div>
            <div style={{ fontSize: 14, fontWeight: 700, color: "#1a1d23", fontFamily: "'JetBrains Mono', monospace", marginTop: 1 }}>{(total / 1e6).toFixed(1)}М</div>
            <div style={{ fontSize: 10, color: "#98a2b3" }}>₽</div>
          </>
        )}
      </div>
    </div>
  );
}

/* ====== BAR CHART ====== */
function BarChart({ data }) {
  const maxVal = Math.max(...data.map(d => Math.max(d.inc, d.exp)), 1);
  const [tooltip, setTooltip] = useState(null);
  const [mounted, setMounted] = useState(false);
  useEffect(() => { const t = setTimeout(() => setMounted(true), 100); return () => clearTimeout(t); }, []);
  return (
    <div style={{ position: "relative" }}>
      <div style={{ display: "flex", alignItems: "flex-end", gap: 5, height: 160, padding: "0 4px" }}>
        {data.map((d, i) => (
          <div key={i} style={{ flex: 1, display: "flex", flexDirection: "column", alignItems: "center", gap: 2, height: "100%" }}
            onMouseEnter={() => setTooltip(i)} onMouseLeave={() => setTooltip(null)}>
            <div style={{ flex: 1, width: "100%", display: "flex", flexDirection: "column", justifyContent: "flex-end", gap: 2, position: "relative" }}>
              {tooltip === i && (d.inc > 0 || d.exp > 0) && (
                <div style={{ position:"absolute", bottom:"calc(100% + 8px)", left:"50%", transform:"translateX(-50%)", background:"#1a1d23", color:"#fff", borderRadius:9, padding:"7px 11px", fontSize:10, whiteSpace:"nowrap", zIndex:10, pointerEvents:"none", boxShadow:"0 4px 16px rgba(0,0,0,.25)", animation:"fadeInUp .15s ease" }}>
                  {d.inc > 0 && <div style={{ color:"#4ade80", marginBottom: d.exp > 0 ? 2 : 0 }}>▲ {fmtAbs(d.inc)} ₽</div>}
                  {d.exp > 0 && <div style={{ color:"#f87171" }}>▼ {fmtAbs(d.exp)} ₽</div>}
                  <div style={{ position:"absolute", bottom:-5, left:"50%", transform:"translateX(-50%)", width:0, height:0, borderLeft:"5px solid transparent", borderRight:"5px solid transparent", borderTop:"5px solid #1a1d23" }} />
                </div>
              )}
              {d.inc > 0 && (
                <div style={{ width:"100%", height: mounted ? `${(d.inc/maxVal)*140}px` : "2px", background:"linear-gradient(180deg,#4ade80,#22c55e)", borderRadius:"4px 4px 0 0", opacity: tooltip === i ? 1 : 0.8, transition:"height .7s cubic-bezier(.34,1.06,.64,1), opacity .2s", transitionDelay:`${i * 0.04}s`, minHeight:2 }} />
              )}
              {d.exp > 0 && (
                <div style={{ width:"100%", height: mounted ? `${(d.exp/maxVal)*140}px` : "2px", background:"linear-gradient(180deg,#f87171,#ef4444)", borderRadius:"4px 4px 0 0", opacity: tooltip === i ? 1 : 0.7, transition:"height .7s cubic-bezier(.34,1.06,.64,1), opacity .2s", transitionDelay:`${i * 0.04}s`, minHeight:2 }} />
              )}
              {d.inc === 0 && d.exp === 0 && <div style={{ width:"100%", height:2, background:"#e8eaed", borderRadius:2 }} />}
            </div>
            <div style={{ fontSize:9, color:"#98a2b3", fontWeight:500, whiteSpace:"nowrap" }}>{d.label}</div>
          </div>
        ))}
      </div>
      <div style={{ display:"flex", gap:16, marginTop:12, justifyContent:"center" }}>
        {[{color:"#22c55e",label:"Доходы"},{color:"#ef4444",label:"Расходы"}].map(l => (
          <div key={l.label} style={{ display:"flex", alignItems:"center", gap:5 }}>
            <div style={{ width:8, height:8, borderRadius:2, background:l.color }} />
            <span style={{ fontSize:11, color:"#667085" }}>{l.label}</span>
          </div>
        ))}
      </div>
    </div>
  );
}

/* ====== PERIOD TABS ====== */
function PeriodTabs({ active, onChange }) {
  return (
    <div style={{ display:"flex", gap:2, background:"#f0f2f5", borderRadius:10, padding:3 }}>
      {["Сегодня","Неделя","Месяц","Квартал","Год"].map(p => (
        <button key={p} onClick={() => onChange(p)} style={{ padding:"6px 12px", borderRadius:7, fontSize:12, fontWeight:500, border:"none", cursor:"pointer", fontFamily:"inherit", transition:"all .2s", background: active === p ? "#fff" : "transparent", color: active === p ? "#1a1d23" : "#667085", boxShadow: active === p ? "0 1px 4px rgba(0,0,0,.08)" : "none" }}>{p}</button>
      ))}
    </div>
  );
}

/* ====== EXPORT BUTTONS ====== */
function ExportBtns() {
  return (
    <div style={{ display:"flex", gap:6 }}>
      {[{icon:IC.pdf,label:"PDF",color:"#ef4444"},{icon:IC.excel,label:"Excel",color:"#16a34a"}].map(b => (
        <button key={b.label} style={{ display:"flex", alignItems:"center", gap:5, padding:"7px 12px", borderRadius:8, border:"1px solid #e8eaed", background:"#fff", fontSize:11, fontWeight:600, color:"#344054", cursor:"pointer", fontFamily:"inherit", transition:"all .15s" }}
          onMouseEnter={e => { e.currentTarget.style.borderColor = b.color; e.currentTarget.style.color = b.color; }}
          onMouseLeave={e => { e.currentTarget.style.borderColor = "#e8eaed"; e.currentTarget.style.color = "#344054"; }}>
          <span style={{ display:"flex" }}>{b.icon}</span>{b.label}
        </button>
      ))}
    </div>
  );
}

/* ====== SORT ICON ====== */
function SortIcon({ col, sortCol, sortDir }) {
  if (sortCol !== col) return <span style={{ opacity:.3, display:"inline-flex", flexDirection:"column", gap:1 }}><span style={{ display:"flex" }}>{IC.sortU}</span><span style={{ display:"flex" }}>{IC.sortD}</span></span>;
  return <span style={{ color:"#22c55e", display:"inline-flex" }}>{sortDir === "asc" ? IC.sortU : IC.sortD}</span>;
}

/* ====== FLOATING TOTALS BAR ====== */
function FloatingTotals({ inc, exp, diff, show }) {
  return (
    <div style={{ position:"sticky", bottom:0, left:0, right:0, zIndex:20, transition:"all .3s ease", transform: show ? "translateY(0)" : "translateY(100%)", opacity: show ? 1 : 0, pointerEvents: show ? "auto" : "none" }}>
      <div style={{ margin:"0 0 0 0", background:"linear-gradient(135deg,#1a1d23,#24272e)", borderRadius:"14px 14px 0 0", padding:"12px 24px", display:"flex", gap:24, alignItems:"center", justifyContent:"space-between", boxShadow:"0 -4px 24px rgba(0,0,0,.15)" }}>
        <div style={{ fontSize:11, fontWeight:500, color:"rgba(255,255,255,.45)", textTransform:"uppercase", letterSpacing:".5px" }}>Итого по фильтру</div>
        <div style={{ display:"flex", gap:28 }}>
          {[
            {label:"Доходы", val:"+" + fmtAbs(inc) + " ₽", color:"#4ade80"},
            {label:"Расходы", val:"−" + fmtAbs(exp) + " ₽", color:"#f87171"},
            {label:"Разница", val:fmt(diff) + " ₽", color: diff >= 0 ? "#4ade80" : "#f87171"},
          ].map(s => (
            <div key={s.label} style={{ textAlign:"center" }}>
              <div style={{ fontSize:10, color:"rgba(255,255,255,.4)", marginBottom:2 }}>{s.label}</div>
              <div style={{ fontSize:13, fontWeight:700, fontFamily:"'JetBrains Mono', monospace", color:s.color }}>{s.val}</div>
            </div>
          ))}
        </div>
        <div style={{ fontSize:11, color:"rgba(255,255,255,.3)" }}>прокрутите вверх ↑</div>
      </div>
    </div>
  );
}

/* ====== SCREEN: BALANCE ====== */
function BalanceScreen() {
  const [period, setPeriod] = useState("Месяц");
  const totalInc = 16175210, totalExp = 12483116;
  const balance = totalInc - totalExp;
  return (
    <div style={{ display:"flex", flexDirection:"column", gap:16 }}>
      <div style={{ display:"flex", justifyContent:"space-between", alignItems:"center", flexWrap:"wrap", gap:10 }}>
        <PeriodTabs active={period} onChange={setPeriod} />
        <ExportBtns />
      </div>
      <div style={{ display:"grid", gridTemplateColumns:"repeat(4,1fr)", gap:12 }}>
        <div style={{ background:"linear-gradient(135deg,#f0fdf4,#dcfce7)", borderRadius:14, border:"1px solid #bbf7d0", padding:"16px 20px", animation:"cardIn .4s ease both", animationDelay:"0s" }}>
          <div style={{ fontSize:10, fontWeight:600, color:"#15803d", textTransform:"uppercase", letterSpacing:".5px", marginBottom:4 }}>Баланс</div>
          <div style={{ fontSize:22, fontWeight:700, color:"#15803d" }}>
            <AnimNum value={balance} color="#15803d" />
          </div>
          <div style={{ fontSize:11, color:"#16a34a", marginTop:2, opacity:.7 }}>за выбранный период</div>
        </div>
        {[
          {label:"Доходы",   val:totalInc, color:"#16a34a", sign:"+", delay:"0.08s"},
          {label:"Расходы",  val:totalExp, color:"#ef4444", sign:"−", delay:"0.14s"},
          {label:"Операций", val:null,     color:"#1a1d23", sign:"",  delay:"0.2s"},
        ].map((c,i) => (
          <div key={i} style={{ background:"#fff", borderRadius:14, border:"1px solid #e8eaed", padding:"16px 20px", animation:"cardIn .4s ease both", animationDelay:c.delay }}>
            <div style={{ fontSize:10, fontWeight:600, color:"#98a2b3", textTransform:"uppercase", letterSpacing:".5px", marginBottom:4 }}>{c.label}</div>
            {c.val !== null
              ? <div style={{ fontSize:22, fontWeight:700 }}><AnimNum value={c.val} prefix={c.sign} color={c.color} /></div>
              : <>
                  <div style={{ fontSize:22, fontWeight:700, color:"#1a1d23" }}>15</div>
                  <div style={{ fontSize:11, color:"#98a2b3", marginTop:2 }}>9 доходов · 6 расходов</div>
                </>
            }
          </div>
        ))}
      </div>
      <div style={{ background:"#fff", borderRadius:16, border:"1px solid #e8eaed", padding:"20px 24px", animation:"cardIn .4s ease .25s both" }}>
        <h3 style={{ fontSize:14, fontWeight:600, color:"#1a1d23", marginBottom:16 }}>Динамика за период</h3>
        <BarChart data={periodBars} />
      </div>
      <div style={{ background:"#fff", borderRadius:16, border:"1px solid #e8eaed", padding:"20px 24px", animation:"cardIn .4s ease .3s both" }}>
        <h3 style={{ fontSize:14, fontWeight:600, color:"#1a1d23", marginBottom:14 }}>Сводка</h3>
        <div style={{ display:"grid", gridTemplateColumns:"repeat(3,1fr)" }}>
          {[
            {label:"Начальный баланс", value:0, color:"#1a1d23"},
            {label:"Конечный баланс",  value:balance, color:"#15803d"},
            {label:"Прирост",          value:balance, color:"#15803d"},
          ].map((s,i) => (
            <div key={i} style={{ padding:"12px 20px", borderRight: i<2 ? "1px solid #f0f2f5" : "none" }}>
              <div style={{ fontSize:11, color:"#98a2b3", marginBottom:4 }}>{s.label}</div>
              <div style={{ fontSize:18, fontWeight:700 }}><AnimNum value={s.value} color={s.color} /></div>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}

/* ====== SCREEN: CATEGORIES ====== */
function CategoriesScreen({ onDrillDown }) {
  const [period, setPeriod] = useState("Месяц");
  const [mode, setMode]     = useState("expense");
  const [prevMode, setPrevMode] = useState("expense");
  const [animDir, setAnimDir]   = useState(1);

  const switchMode = (m) => {
    if (m === mode) return;
    setAnimDir(m === "income" ? 1 : -1);
    setPrevMode(mode);
    setMode(m);
  };

  const activeTx = transactions.filter(t => t.type === mode);
  const catMap = {};
  activeTx.forEach(t => { if (!catMap[t.cat]) catMap[t.cat] = 0; catMap[t.cat] += Math.abs(t.amount); });
  const catData = Object.entries(catMap).map(([label,value]) => ({ label, value, color: catColors[label] || "#94a3b8" })).sort((a,b) => b.value - a.value);
  const total = catData.reduce((s,d) => s + d.value, 0);

  return (
    <div style={{ display:"flex", flexDirection:"column", gap:16 }}>
      <div style={{ display:"flex", justifyContent:"space-between", alignItems:"center", flexWrap:"wrap", gap:10 }}>
        <PeriodTabs active={period} onChange={setPeriod} />
        <ExportBtns />
      </div>

      <div style={{ display:"flex", background:"#f0f2f5", borderRadius:11, padding:3, width:"fit-content" }}>
        {[{k:"expense",l:"Расходы"},{k:"income",l:"Доходы"}].map(m => (
          <button key={m.k} onClick={() => switchMode(m.k)} style={{ padding:"8px 20px", borderRadius:8, fontSize:13, fontWeight:500, border:"none", cursor:"pointer", fontFamily:"inherit", transition:"all .25s", background: mode === m.k ? (m.k === "expense" ? "#ef4444" : "#22c55e") : "transparent", color: mode === m.k ? "#fff" : "#667085", transform: mode === m.k ? "scale(1.02)" : "scale(1)" }}>{m.l}</button>
        ))}
      </div>

      <div style={{ display:"grid", gridTemplateColumns:"repeat(3,1fr)", gap:12 }}>
        {[
          {label:"Общая сумма", value:total, color: mode === "expense" ? "#ef4444" : "#16a34a"},
          {label:"Операций",   value:activeTx.length, color:"#1a1d23", raw:true},
          {label:"Категорий",  value:catData.length,  color:"#1a1d23", raw:true},
        ].map((s,i) => (
          <div key={i} style={{ background:"#fff", borderRadius:14, border:"1px solid #e8eaed", padding:"14px 18px", animation:"cardIn .35s ease both", animationDelay:`${i*0.07}s` }}>
            <div style={{ fontSize:10, color:"#98a2b3", fontWeight:500, textTransform:"uppercase", letterSpacing:".5px", marginBottom:6 }}>{s.label}</div>
            {s.raw
              ? <div style={{ fontSize:20, fontWeight:700, fontFamily:"'JetBrains Mono', monospace", color:s.color }}>{s.value}</div>
              : <div style={{ fontSize:20, fontWeight:700 }}><AnimNum value={s.value} color={s.color} /></div>
            }
          </div>
        ))}
      </div>

      <div style={{ display:"grid", gridTemplateColumns:"220px 1fr", gap:16 }}>
        {/* Donut */}
        <div style={{ background:"#fff", borderRadius:16, border:"1px solid #e8eaed", padding:"20px 20px", display:"flex", flexDirection:"column", alignItems:"center", gap:12 }}>
          <h3 style={{ fontSize:14, fontWeight:600, color:"#1a1d23", alignSelf:"flex-start" }}>
            Структура {mode === "expense" ? "расходов" : "доходов"}
          </h3>
          {catData.length > 0
            ? <DonutChart key={mode} data={catData} size={180} thickness={32} onCatClick={cat => onDrillDown && onDrillDown(cat, mode)} />
            : <div style={{ color:"#98a2b3", fontSize:13, padding:"20px 0" }}>Нет данных</div>
          }
          <div style={{ fontSize:11, color:"#98a2b3", textAlign:"center", marginTop:-4 }}>Нажмите на сектор для детализации</div>
        </div>

        {/* List */}
        <div style={{ background:"#fff", borderRadius:16, border:"1px solid #e8eaed", padding:"20px 24px" }}>
          <h3 style={{ fontSize:14, fontWeight:600, color:"#1a1d23", marginBottom:14 }}>Детализация по категориям</h3>
          <div style={{ display:"flex", flexDirection:"column", gap:10 }}>
            {catData.map((c, i) => (
              <div key={c.label} style={{ animation:"rowSlideIn .3s ease both", animationDelay:`${i*0.06}s`, cursor:"pointer" }}
                onClick={() => onDrillDown && onDrillDown(c.label, mode)}
                onMouseEnter={e => e.currentTarget.style.background = "#f8f9fb"}
                onMouseLeave={e => e.currentTarget.style.background = "transparent"}
                style={{ borderRadius:10, padding:"8px 10px", transition:"background .15s", cursor:"pointer", animation:`rowSlideIn .3s ease ${i*0.06}s both` }}>
                <div style={{ display:"flex", justifyContent:"space-between", alignItems:"center", marginBottom:5 }}>
                  <div style={{ display:"flex", alignItems:"center", gap:8 }}>
                    <div style={{ width:10, height:10, borderRadius:3, background:c.color, flexShrink:0 }} />
                    <span style={{ fontSize:13, fontWeight:500, color:"#344054" }}>{c.label}</span>
                    <span style={{ fontSize:11, color:"#98a2b3" }}>{((c.value/total)*100).toFixed(1)}% · {activeTx.filter(t => t.cat===c.label).length} опер.</span>
                  </div>
                  <div style={{ display:"flex", alignItems:"center", gap:6 }}>
                    <span style={{ fontSize:13, fontWeight:600, fontFamily:"'JetBrains Mono', monospace", color: mode === "expense" ? "#ef4444" : "#15803d" }}>{fmtAbs(c.value)} ₽</span>
                    <span style={{ display:"flex", color:"#98a2b3" }}>{IC.drilldown}</span>
                  </div>
                </div>
                <div style={{ height:5, background:"#f0f2f5", borderRadius:3, overflow:"hidden" }}>
                  <div style={{ height:"100%", width:`${(c.value/catData[0].value)*100}%`, background:c.color, borderRadius:3, opacity:.7, transition:"width .8s cubic-bezier(.34,1.06,.64,1)" }} />
                </div>
              </div>
            ))}
          </div>
          <div style={{ marginTop:14, paddingTop:12, borderTop:"1px solid #f0f2f5", display:"flex", justifyContent:"space-between", fontSize:12 }}>
            <span style={{ color:"#667085" }}>Итого {mode === "expense" ? "расходов" : "доходов"}</span>
            <span style={{ fontWeight:700, fontFamily:"'JetBrains Mono', monospace", color: mode === "expense" ? "#ef4444" : "#15803d" }}>{fmtAbs(total)} ₽</span>
          </div>
        </div>
      </div>
    </div>
  );
}

/* ====== SCREEN: DETAILS ====== */
function DetailsScreen({ initCat, initType }) {
  const [period, setPeriod]     = useState("Месяц");
  const [typeFilter, setType]   = useState(initType || "all");
  const [catFilter, setCat]     = useState(initCat  || "all");
  const [search, setSearch]     = useState("");
  const [catOpen, setCatOpen]   = useState(false);
  const [sortCol, setSortCol]   = useState("date");
  const [sortDir, setSortDir]   = useState("desc");
  const [showFloat, setShowFloat] = useState(false);
  const catRef   = useRef(null);
  const tableRef = useRef(null);

  // Close dropdown outside click
  useEffect(() => {
    const h = e => { if (catRef.current && !catRef.current.contains(e.target)) setCatOpen(false); };
    document.addEventListener("mousedown", h);
    return () => document.removeEventListener("mousedown", h);
  }, []);

  // Floating totals on scroll
  useEffect(() => {
    const el = tableRef.current;
    if (!el) return;
    const onScroll = () => setShowFloat(el.scrollTop > 120);
    el.addEventListener("scroll", onScroll);
    return () => el.removeEventListener("scroll", onScroll);
  }, []);

  const allCats = [...new Set(transactions.map(t => t.cat))];

  // Filter
  const filtered = transactions.filter(t => {
    if (typeFilter !== "all" && t.type !== typeFilter) return false;
    if (catFilter  !== "all" && t.cat  !== catFilter)  return false;
    if (search.trim()) {
      const q = search.toLowerCase();
      if (!t.cat.toLowerCase().includes(q) && !t.desc.toLowerCase().includes(q) && !String(Math.abs(t.amount)).includes(q)) return false;
    }
    return true;
  });

  // Sort
  const sorted = [...filtered].sort((a, b) => {
    let av, bv;
    if (sortCol === "date") {
      const parseDate = s => { const [d,m,y] = s.split("."); return new Date(y,m-1,d); };
      av = parseDate(a.date); bv = parseDate(b.date);
    } else if (sortCol === "amount") {
      av = Math.abs(a.amount); bv = Math.abs(b.amount);
    } else if (sortCol === "cat") {
      av = a.cat; bv = b.cat;
    } else if (sortCol === "type") {
      av = a.type; bv = b.type;
    } else {
      av = a[sortCol]; bv = b[sortCol];
    }
    if (av < bv) return sortDir === "asc" ? -1 : 1;
    if (av > bv) return sortDir === "asc" ? 1 : -1;
    return 0;
  });

  const toggleSort = col => {
    if (sortCol === col) setSortDir(d => d === "asc" ? "desc" : "asc");
    else { setSortCol(col); setSortDir("desc"); }
  };

  const totalInc = filtered.filter(t => t.type==="income").reduce((s,t) => s+t.amount, 0);
  const totalExp = filtered.filter(t => t.type==="expense").reduce((s,t) => s+Math.abs(t.amount), 0);
  const diff = totalInc - totalExp;

  const colHead = (label, col, align = "left") => (
    <div onClick={() => toggleSort(col)} style={{ display:"flex", alignItems:"center", gap:4, cursor:"pointer", userSelect:"none", justifyContent: align === "right" ? "flex-end" : "flex-start" }}
      onMouseEnter={e => e.currentTarget.style.color = "#1a1d23"}
      onMouseLeave={e => e.currentTarget.style.color = "#98a2b3"}>
      <span>{label}</span>
      <SortIcon col={col} sortCol={sortCol} sortDir={sortDir} />
    </div>
  );

  return (
    <div style={{ display:"flex", flexDirection:"column", gap:16, position:"relative" }}>
      <div style={{ display:"flex", justifyContent:"space-between", alignItems:"center", flexWrap:"wrap", gap:10 }}>
        <PeriodTabs active={period} onChange={setPeriod} />
        <ExportBtns />
      </div>

      {/* Filters row */}
      <div style={{ display:"flex", alignItems:"center", gap:10, flexWrap:"wrap" }}>
        {/* Search */}
        <div style={{ position:"relative", flex:"1 1 200px", maxWidth:280 }}>
          <span style={{ position:"absolute", left:10, top:"50%", transform:"translateY(-50%)", display:"flex", color:"#98a2b3", pointerEvents:"none" }}>{IC.search}</span>
          <input value={search} onChange={e => setSearch(e.target.value)} placeholder="Поиск по описанию, категории…"
            style={{ width:"100%", paddingLeft:32, paddingRight: search ? 30 : 12, paddingTop:8, paddingBottom:8, borderRadius:9, border:"1px solid #e8eaed", background:"#fff", fontSize:12, fontFamily:"inherit", color:"#1a1d23", outline:"none", transition:"border .2s" }}
            onFocus={e => e.currentTarget.style.borderColor = "#22c55e"}
            onBlur={e => e.currentTarget.style.borderColor = "#e8eaed"}
          />
          {search && <button onClick={() => setSearch("")} style={{ position:"absolute", right:8, top:"50%", transform:"translateY(-50%)", border:"none", background:"none", cursor:"pointer", display:"flex", color:"#98a2b3", padding:2 }}>{IC.close}</button>}
        </div>

        {/* Type toggle */}
        <div style={{ display:"flex", background:"#f0f2f5", borderRadius:9, padding:3, gap:2 }}>
          {[{k:"all",l:"Все"},{k:"income",l:"Доходы"},{k:"expense",l:"Расходы"}].map(f => (
            <button key={f.k} onClick={() => setType(f.k)} style={{ padding:"6px 13px", borderRadius:6, fontSize:12, fontWeight:500, border:"none", cursor:"pointer", fontFamily:"inherit", transition:"all .2s", background: typeFilter === f.k ? (f.k==="income"?"#22c55e":f.k==="expense"?"#ef4444":"#fff") : "transparent", color: typeFilter === f.k ? (f.k==="all"?"#1a1d23":"#fff") : "#667085", boxShadow: typeFilter===f.k&&f.k==="all" ? "0 1px 4px rgba(0,0,0,.08)" : "none" }}>{f.l}</button>
          ))}
        </div>

        {/* Category dropdown */}
        <div ref={catRef} style={{ position:"relative" }}>
          <button onClick={() => setCatOpen(p => !p)} style={{ display:"flex", alignItems:"center", gap:6, padding:"7px 14px", background:"#fff", border:`1px solid ${catOpen?"#22c55e":"#e8eaed"}`, borderRadius:9, fontSize:12, fontWeight:500, color:"#344054", cursor:"pointer", fontFamily:"inherit", transition:"border .2s" }}>
            {catFilter === "all" ? "Все категории" : catFilter}
            {catFilter !== "all" && <span style={{ display:"flex", alignItems:"center", justifyContent:"center", width:16, height:16, borderRadius:"50%", background:"#22c55e", color:"#fff" }} onClick={e => { e.stopPropagation(); setCat("all"); }}>
              <svg width="8" height="8" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="3"><line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/></svg>
            </span>}
            <span style={{ display:"flex", transform: catOpen ? "rotate(180deg)" : "none", transition:"transform .2s" }}>{IC.chevD}</span>
          </button>
          {catOpen && (
            <div style={{ position:"absolute", top:"calc(100% + 6px)", left:0, background:"#fff", border:"1px solid #e8eaed", borderRadius:11, padding:4, zIndex:50, minWidth:190, boxShadow:"0 8px 32px rgba(0,0,0,.1)", animation:"scaleIn .15s ease" }}>
              {["all", ...allCats].map(c => (
                <button key={c} onClick={() => { setCat(c); setCatOpen(false); }} style={{ display:"block", width:"100%", textAlign:"left", padding:"7px 12px", border:"none", background: catFilter===c ? "#f0fdf4" : "transparent", fontSize:12, color: catFilter===c ? "#15803d" : "#344054", cursor:"pointer", fontFamily:"inherit", borderRadius:7, fontWeight: catFilter===c ? 600 : 400, transition:"background .15s" }}
                  onMouseEnter={e => { if(catFilter!==c) e.currentTarget.style.background="#f8f9fb"; }}
                  onMouseLeave={e => { if(catFilter!==c) e.currentTarget.style.background="transparent"; }}>
                  {c === "all" ? "Все категории" : (
                    <span style={{ display:"flex", alignItems:"center", gap:7 }}>
                      <span style={{ width:8, height:8, borderRadius:2, background:catColors[c]||"#94a3b8", flexShrink:0, display:"inline-block" }} />
                      {c}
                    </span>
                  )}
                </button>
              ))}
            </div>
          )}
        </div>

        {/* Active filter indicator */}
        {(catFilter !== "all" || typeFilter !== "all" || search) && (
          <button onClick={() => { setCat("all"); setType("all"); setSearch(""); }} style={{ fontSize:11, color:"#98a2b3", border:"none", background:"none", cursor:"pointer", fontFamily:"inherit", textDecoration:"underline", transition:"color .15s" }}
            onMouseEnter={e => e.currentTarget.style.color="#1a1d23"}
            onMouseLeave={e => e.currentTarget.style.color="#98a2b3"}>
            Сбросить фильтры
          </button>
        )}
      </div>

      {/* Stats cards */}
      <div style={{ display:"grid", gridTemplateColumns:"repeat(4,1fr)", gap:12 }}>
        {[
          {label:"Операций", val:filtered.length, raw:true, color:"#1a1d23"},
          {label:"Доходы",   val:totalInc, color:"#16a34a"},
          {label:"Расходы",  val:totalExp, color:"#ef4444"},
          {label:"Разница",  val:diff,     color: diff>=0?"#16a34a":"#ef4444"},
        ].map((s,i) => (
          <div key={i} style={{ background:"#fff", borderRadius:14, border:"1px solid #e8eaed", padding:"14px 18px", animation:"cardIn .35s ease both", animationDelay:`${i*0.06}s` }}>
            <div style={{ fontSize:10, color:"#98a2b3", fontWeight:500, textTransform:"uppercase", letterSpacing:".5px", marginBottom:6 }}>{s.label}</div>
            {s.raw
              ? <div style={{ fontSize:18, fontWeight:700, fontFamily:"'JetBrains Mono', monospace", color:s.color }}>{s.val}</div>
              : <div style={{ fontSize:18, fontWeight:700 }}><AnimNum value={s.val} color={s.color} /></div>
            }
          </div>
        ))}
      </div>

      {/* Table wrapper — this div gets scrolled, used for floating totals trigger */}
      <div ref={tableRef} style={{ background:"#fff", borderRadius:16, border:"1px solid #e8eaed", overflow:"hidden", maxHeight:"calc(100vh - 400px)", display:"flex", flexDirection:"column" }}>
        {/* Sticky header */}
        <div style={{ display:"grid", gridTemplateColumns:"110px 80px 1fr 180px 130px 140px", padding:"11px 20px", borderBottom:"1px solid #f0f2f5", background:"#fafafa", position:"sticky", top:0, zIndex:5 }}>
          {[
            {label:"Дата",     col:"date",   align:"left"},
            {label:"Тип",      col:"type",   align:"left"},
            {label:"Категория",col:"cat",    align:"left"},
            {label:"Описание", col:"desc",   align:"left"},
            {label:"Касса",    col:"wallet", align:"left"},
            {label:"Сумма",    col:"amount", align:"right"},
          ].map(h => (
            <div key={h.col} style={{ fontSize:10, fontWeight:600, color:"#98a2b3", textTransform:"uppercase", letterSpacing:".5px" }}>
              {colHead(h.label, h.col, h.align)}
            </div>
          ))}
        </div>

        {/* Scrollable body */}
        <div style={{ overflowY:"auto", flex:1 }} onScroll={e => setShowFloat(e.currentTarget.scrollTop > 80)}>
          {sorted.length === 0 && (
            <div style={{ padding:"48px 20px", textAlign:"center" }}>
              <div style={{ fontSize:32, marginBottom:8 }}>🔍</div>
              <div style={{ fontSize:14, fontWeight:500, color:"#344054", marginBottom:4 }}>Ничего не найдено</div>
              <div style={{ fontSize:12, color:"#98a2b3" }}>Попробуйте изменить фильтры или поисковый запрос</div>
            </div>
          )}

          {sorted.map((tx, i) => (
            <div key={tx.id} style={{ display:"grid", gridTemplateColumns:"110px 80px 1fr 180px 130px 140px", padding:"11px 20px", alignItems:"center", borderBottom:"1px solid #f8f9fb", transition:"background .15s", animation:`rowSlideIn .25s ease ${Math.min(i*0.04,0.4)}s both`, cursor:"default" }}
              onMouseEnter={e => e.currentTarget.style.background="#f8f9fb"}
              onMouseLeave={e => e.currentTarget.style.background="transparent"}>

              {/* Date */}
              <div style={{ fontSize:12, color:"#667085" }}>{tx.date}</div>

              {/* Type badge */}
              <div>
                <span style={{ fontSize:11, fontWeight:600, padding:"3px 8px", borderRadius:6, background: tx.type==="income"?"#f0fdf4":"#fef2f2", color: tx.type==="income"?"#16a34a":"#ef4444" }}>
                  {tx.type==="income" ? "Доход" : "Расход"}
                </span>
              </div>

              {/* Category */}
              <div style={{ display:"flex", alignItems:"center", gap:7 }}>
                <div style={{ width:8, height:8, borderRadius:2, background:catColors[tx.cat]||"#94a3b8", flexShrink:0 }} />
                <span style={{ fontSize:13, fontWeight:500, color:"#344054" }}>{tx.cat}</span>
              </div>

              {/* Description */}
              <div style={{ fontSize:12, color:"#667085", overflow:"hidden", textOverflow:"ellipsis", whiteSpace:"nowrap" }} title={tx.desc}>
                {search && tx.desc.toLowerCase().includes(search.toLowerCase())
                  ? <span dangerouslySetInnerHTML={{ __html: tx.desc.replace(new RegExp(`(${search})`, "gi"), '<mark style="background:#fef9c3;border-radius:2px;padding:0 1px">$1</mark>') }} />
                  : tx.desc || <span style={{ color:"#d0d5dd", fontStyle:"italic" }}>—</span>
                }
              </div>

              {/* Wallet */}
              <div style={{ display:"flex", alignItems:"center", gap:5, color:"#667085" }}>
                <span style={{ display:"flex" }}>{tx.wallet==="cash" ? IC.cash : IC.card}</span>
                <span style={{ fontSize:12 }}>{tx.wallet==="cash" ? "Наличные" : "Безналичные"}</span>
              </div>

              {/* Amount */}
              <div style={{ fontSize:13, fontWeight:700, fontFamily:"'JetBrains Mono', monospace", textAlign:"right", color: tx.type==="income"?"#16a34a":"#ef4444" }}>
                {tx.type==="income" ? "+" : "−"}{fmtAbs(tx.amount)} ₽
              </div>
            </div>
          ))}
        </div>

        {/* Floating totals */}
        <FloatingTotals inc={totalInc} exp={totalExp} diff={diff} show={showFloat} />
      </div>
    </div>
  );
}

/* ====== DRILL-DOWN WRAPPER ====== */
function DrillDownDetails({ cat, type, onBack }) {
  return (
    <div style={{ animation:"slideInRight .3s cubic-bezier(.34,1.06,.64,1)" }}>
      <button onClick={onBack} style={{ display:"flex", alignItems:"center", gap:7, marginBottom:16, padding:"7px 14px", borderRadius:9, border:"1px solid #e8eaed", background:"#fff", fontSize:13, fontWeight:500, color:"#344054", cursor:"pointer", fontFamily:"inherit", transition:"all .2s" }}
        onMouseEnter={e => { e.currentTarget.style.borderColor="#22c55e"; e.currentTarget.style.color="#22c55e"; }}
        onMouseLeave={e => { e.currentTarget.style.borderColor="#e8eaed"; e.currentTarget.style.color="#344054"; }}>
        {IC.back}
        <span>Назад к структуре</span>
        <span style={{ marginLeft:4, padding:"2px 8px", borderRadius:5, background: type==="expense"?"#fef2f2":"#f0fdf4", color: type==="expense"?"#ef4444":"#16a34a", fontSize:11, fontWeight:600 }}>{cat}</span>
      </button>
      <DetailsScreen initCat={cat} initType={type} />
    </div>
  );
}

/* ====== MAIN APP ====== */
export default function ReportsApp() {
  const [activeTab, setActiveTab] = useState("balance");
  const [drillCat,  setDrillCat]  = useState(null);
  const [drillType, setDrillType] = useState("expense");

  const handleDrillDown = useCallback((cat, type) => {
    setDrillCat(cat);
    setDrillType(type);
    setActiveTab("details");
  }, []);

  const handleTabChange = (tab) => {
    setActiveTab(tab);
    if (tab !== "details") setDrillCat(null);
  };

  const tabs = [
    {k:"balance",    l:"Баланс за период",       icon:IC.balance},
    {k:"categories", l:"Структура по категориям", icon:IC.pie},
    {k:"details",    l:"Детализация операций",    icon:IC.list},
  ];

  return (
    <div style={{ display:"flex", height:"100vh", fontFamily:"'DM Sans','Segoe UI',sans-serif", background:"#f0f2f5", color:"#1a1d23", overflow:"hidden" }}>
      <style>{`
        @import url('https://fonts.googleapis.com/css2?family=DM+Sans:ital,opsz,wght@0,9..40,300;0,9..40,400;0,9..40,500;0,9..40,600;0,9..40,700&family=JetBrains+Mono:wght@400;500;600;700&display=swap');
        * { box-sizing: border-box; margin: 0; padding: 0; }
        ::-webkit-scrollbar { width: 5px; }
        ::-webkit-scrollbar-track { background: transparent; }
        ::-webkit-scrollbar-thumb { background: #d0d5dd; border-radius: 3px; }
        @keyframes fadeIn       { from { opacity:0; transform:translateY(6px); } to { opacity:1; transform:translateY(0); } }
        @keyframes fadeInUp     { from { opacity:0; transform:translateX(-50%) translateY(6px); } to { opacity:1; transform:translateX(-50%) translateY(0); } }
        @keyframes scaleIn      { from { opacity:0; transform:scale(.94); } to { opacity:1; transform:scale(1); } }
        @keyframes cardIn       { from { opacity:0; transform:translateY(14px); } to { opacity:1; transform:translateY(0); } }
        @keyframes rowSlideIn   { from { opacity:0; transform:translateX(-8px); } to { opacity:1; transform:translateX(0); } }
        @keyframes slideInRight { from { opacity:0; transform:translateX(30px); } to { opacity:1; transform:translateX(0); } }
      `}</style>

      {/* Sidebar */}
      <nav style={{ width:64, background:"linear-gradient(180deg,#1a1d23,#24272e)", display:"flex", flexDirection:"column", flexShrink:0, zIndex:10 }}>
        <div style={{ padding:"18px 0", display:"flex", alignItems:"center", justifyContent:"center", borderBottom:"1px solid rgba(255,255,255,.06)" }}>
          <div style={{ width:34, height:34, borderRadius:9, background:"linear-gradient(135deg,#22c55e,#16a34a)", display:"flex", alignItems:"center", justifyContent:"center", fontSize:15, fontWeight:700, color:"#fff" }}>₽</div>
        </div>
        <div style={{ padding:"10px 7px", flex:1, display:"flex", flexDirection:"column", gap:2 }}>
          {[
            {icon:<svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="M3 9l9-7 9 7v11a2 2 0 01-2 2H5a2 2 0 01-2-2z"/><polyline points="9 22 9 12 15 12 15 22"/></svg>,active:false},
            {icon:<svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><line x1="8" y1="6" x2="21" y2="6"/><line x1="8" y1="12" x2="21" y2="12"/><line x1="8" y1="18" x2="21" y2="18"/><line x1="3" y1="6" x2="3.01" y2="6"/><line x1="3" y1="12" x2="3.01" y2="12"/><line x1="3" y1="18" x2="3.01" y2="18"/></svg>,active:false},
            {icon:<svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><line x1="18" y1="20" x2="18" y2="10"/><line x1="12" y1="20" x2="12" y2="4"/><line x1="6" y1="20" x2="6" y2="14"/></svg>,active:true},
            {icon:<svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="M20.59 13.41l-7.17 7.17a2 2 0 01-2.83 0L2 12V2h10l8.59 8.59a2 2 0 010 2.82z"/><line x1="7" y1="7" x2="7.01" y2="7"/></svg>,active:false},
            {icon:<svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><circle cx="12" cy="12" r="3"/><path d="M19.4 15a1.65 1.65 0 00.33 1.82l.06.06a2 2 0 010 2.83 2 2 0 01-2.83 0l-.06-.06a1.65 1.65 0 00-1.82-.33 1.65 1.65 0 00-1 1.51V21a2 2 0 01-4 0v-.09A1.65 1.65 0 009 19.4a1.65 1.65 0 00-1.82.33l-.06.06a2 2 0 01-2.83-2.83l.06-.06A1.65 1.65 0 004.68 15a1.65 1.65 0 00-1.51-1H3a2 2 0 010-4h.09A1.65 1.65 0 004.6 9a1.65 1.65 0 00-.33-1.82l-.06-.06a2 2 0 012.83-2.83l.06.06A1.65 1.65 0 009 4.68a1.65 1.65 0 001-1.51V3a2 2 0 014 0v.09a1.65 1.65 0 001 1.51 1.65 1.65 0 001.82-.33l.06-.06a2 2 0 012.83 2.83l-.06.06A1.65 1.65 0 0019.4 9a1.65 1.65 0 001.51 1H21a2 2 0 010 4h-.09a1.65 1.65 0 00-1.51 1z"/></svg>,active:false},
          ].map((item,i) => (
            <div key={i} style={{ display:"flex", alignItems:"center", justifyContent:"center", padding:"9px 0", borderRadius:9, cursor:"pointer", transition:"all .2s", color: item.active?"#fff":"rgba(255,255,255,.45)", background: item.active?"rgba(255,255,255,.12)":"transparent", position:"relative" }}>
              {item.active && <div style={{ position:"absolute", left:0, top:"50%", transform:"translateY(-50%)", width:3, height:22, background:"#22c55e", borderRadius:"0 3px 3px 0" }} />}
              {item.icon}
            </div>
          ))}
        </div>
      </nav>

      {/* Main */}
      <main style={{ flex:1, display:"flex", flexDirection:"column", overflow:"hidden" }}>
        {/* Top bar */}
        <div style={{ padding:"16px 24px", background:"#fff", borderBottom:"1px solid #e8eaed", display:"flex", alignItems:"center", justifyContent:"space-between", flexShrink:0 }}>
          <div>
            <h1 style={{ fontSize:20, fontWeight:700, color:"#1a1d23" }}>Отчёты</h1>
            <p style={{ fontSize:12, color:"#98a2b3", marginTop:1 }}>01.02.2026 — 22.02.2026</p>
          </div>
        </div>

        {/* Tab navigation */}
        <div style={{ padding:"0 24px", background:"#fff", borderBottom:"1px solid #e8eaed", display:"flex", flexShrink:0 }}>
          {tabs.map(tab => (
            <button key={tab.k} onClick={() => handleTabChange(tab.k)} style={{ display:"flex", alignItems:"center", gap:7, padding:"13px 18px", border:"none", borderBottom: activeTab===tab.k ? "2px solid #22c55e" : "2px solid transparent", background:"transparent", cursor:"pointer", fontFamily:"inherit", fontSize:13, fontWeight: activeTab===tab.k ? 600 : 400, color: activeTab===tab.k ? "#1a1d23" : "#667085", transition:"all .2s", marginBottom:-1 }}>
              <span style={{ display:"flex", color: activeTab===tab.k ? "#22c55e" : "#98a2b3", transition:"color .2s" }}>{tab.icon}</span>
              {tab.l}
              {tab.k === "details" && drillCat && (
                <span style={{ fontSize:10, padding:"2px 7px", borderRadius:5, background:"#f0fdf4", color:"#16a34a", fontWeight:600, marginLeft:2 }}>{drillCat}</span>
              )}
            </button>
          ))}
        </div>

        {/* Content */}
        <div style={{ flex:1, overflowY: activeTab === "details" ? "hidden" : "auto", padding:"20px 24px" }}>
          <div key={activeTab} style={{ animation:"fadeIn .25s ease", maxWidth:1200 }}>
            {activeTab === "balance"    && <BalanceScreen />}
            {activeTab === "categories" && <CategoriesScreen onDrillDown={handleDrillDown} />}
            {activeTab === "details"    && (
              drillCat
                ? <DrillDownDetails cat={drillCat} type={drillType} onBack={() => { setDrillCat(null); setActiveTab("categories"); }} />
                : <DetailsScreen />
            )}
          </div>
        </div>
      </main>
    </div>
  );
}
