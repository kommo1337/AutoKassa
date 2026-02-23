import { useState, useRef, useEffect, useCallback, useMemo } from "react";

/* ====== HELPERS ====== */
const fm = n => { const a = Math.abs(n); return (n > 0 ? "+" : n < 0 ? "−" : "") + a.toLocaleString("ru-RU"); };
const fmtD = d => `${String(d.getDate()).padStart(2,"0")}.${String(d.getMonth()+1).padStart(2,"0")}.${d.getFullYear()}`;
const today = new Date();
const todayStr = fmtD(today);

/* ====== DATA ====== */
const initialTx = [
  {id:1,  date:"23.02.2026",type:"expense",cat:"Аренда",          amount:-10000, wallet:"card", desc:"Аренда офиса"},
  {id:2,  date:"23.02.2026",type:"expense",cat:"Аренда",          amount:-7800,  wallet:"card", desc:"Аренда склада"},
  {id:3,  date:"23.02.2026",type:"income", cat:"Диагностика",     amount:12222,  wallet:"card", desc:""},
  {id:4,  date:"23.02.2026",type:"income", cat:"Диагностика",     amount:5220,   wallet:"card", desc:""},
  {id:5,  date:"21.02.2026",type:"expense",cat:"Запчасти",        amount:-18200, wallet:"cash", desc:""},
  {id:6,  date:"21.02.2026",type:"income", cat:"Оплата за ТО",    amount:45000,  wallet:"card", desc:""},
  {id:7,  date:"21.02.2026",type:"expense",cat:"Расходники",      amount:-3400,  wallet:"cash", desc:""},
  {id:8,  date:"20.02.2026",type:"expense",cat:"Аванс",           amount:-185000,wallet:"cash", desc:""},
  {id:9,  date:"20.02.2026",type:"income", cat:"Бюджет начальный",amount:250000, wallet:"cash", desc:""},
  {id:10, date:"20.02.2026",type:"income", cat:"Бюджет начальный",amount:744,    wallet:"card", desc:""},
  {id:11, date:"20.02.2026",type:"expense",cat:"Аванс",           amount:-14,    wallet:"cash", desc:""},
  {id:12, date:"19.02.2026",type:"expense",cat:"Аванс",           amount:-100000,wallet:"card", desc:""},
  {id:13, date:"19.02.2026",type:"expense",cat:"Зарплата",        amount:-55000, wallet:"cash", desc:""},
  {id:14, date:"19.02.2026",type:"income", cat:"Оплата за ТО",    amount:32000,  wallet:"card", desc:""},
  {id:15, date:"18.02.2026",type:"expense",cat:"Аренда",          amount:-34888, wallet:"cash", desc:""},
  {id:16, date:"18.02.2026",type:"expense",cat:"Запчасти",        amount:-12500, wallet:"cash", desc:""},
  {id:17, date:"18.02.2026",type:"income", cat:"Оплата за ТО",    amount:45000,  wallet:"card", desc:""},
  {id:18, date:"17.02.2026",type:"expense",cat:"Коммунальные",    amount:-8700,  wallet:"card", desc:""},
  {id:19, date:"17.02.2026",type:"expense",cat:"Расходники",      amount:-4200,  wallet:"cash", desc:""},
  {id:20, date:"15.02.2026",type:"income", cat:"Диагностика",     amount:9800,   wallet:"cash", desc:""},
  {id:21, date:"15.02.2026",type:"expense",cat:"Налоги",          amount:-22000, wallet:"card", desc:""},
  {id:22, date:"14.02.2026",type:"income", cat:"Оплата за ТО",    amount:18000,  wallet:"card", desc:""},
  {id:23, date:"14.02.2026",type:"expense",cat:"Транспорт",       amount:-3500,  wallet:"cash", desc:""},
  {id:24, date:"12.02.2026",type:"expense",cat:"Реклама",         amount:-15000, wallet:"card", desc:""},
  {id:25, date:"10.02.2026",type:"income", cat:"Бюджет начальный",amount:80000,  wallet:"cash", desc:""},
  {id:26, date:"10.02.2026",type:"expense",cat:"Зарплата",        amount:-55000, wallet:"card", desc:""},
  {id:27, date:"08.02.2026",type:"expense",cat:"Аренда",          amount:-12000, wallet:"card", desc:""},
  {id:28, date:"05.02.2026",type:"income", cat:"Диагностика",     amount:7500,   wallet:"cash", desc:""},
  {id:29, date:"03.02.2026",type:"expense",cat:"Запчасти",        amount:-9800,  wallet:"cash", desc:""},
  {id:30, date:"01.02.2026",type:"income", cat:"Оплата за ТО",    amount:25000,  wallet:"card", desc:""},
];

const PALETTE = ["#6366f1","#f59e0b","#ec4899","#8b5cf6","#14b8a6","#f97316","#06b6d4","#84cc16","#ef4444","#64748b","#3b82f6","#94a3b8"];
const initCats = [
  {id:1,name:"Аванс",color:"#6366f1"},{id:2,name:"Запчасти",color:"#f59e0b"},{id:3,name:"Зарплата",color:"#ec4899"},
  {id:4,name:"Аренда",color:"#8b5cf6"},{id:5,name:"Инструменты",color:"#14b8a6"},{id:6,name:"Расходники",color:"#f97316"},
  {id:7,name:"Коммунальные",color:"#06b6d4"},{id:8,name:"Реклама",color:"#84cc16"},{id:9,name:"Налоги",color:"#ef4444"},
  {id:10,name:"Страховка",color:"#64748b"},{id:11,name:"Транспорт",color:"#3b82f6"},{id:12,name:"Другое",color:"#94a3b8"},
];
const extraCatColors = {"Оплата за ТО":"#22c55e","Бюджет начальный":"#10b981","Диагностика":"#0ea5e9"};
const navItems = [
  {icon:"home",label:"Главная",active:false},
  {icon:"list",label:"Операции",active:true},
  {icon:"chart",label:"Отчёты",active:false},
  {icon:"settings",label:"Настройки",active:false},
];

/* ====== DATE PRESETS ====== */
const getPresetRange = (key) => {
  const t = new Date(); t.setHours(0,0,0,0);
  if(key==="today"){return{from:new Date(t),to:new Date(t)};}
  if(key==="week"){const f=new Date(t);f.setDate(t.getDate()-t.getDay()+1);return{from:f,to:new Date(t)};}
  if(key==="month"){return{from:new Date(t.getFullYear(),t.getMonth(),1),to:new Date(t)};}
  if(key==="prev_month"){const f=new Date(t.getFullYear(),t.getMonth()-1,1);const to=new Date(t.getFullYear(),t.getMonth(),0);return{from:f,to};}
  if(key==="quarter"){const qStart=Math.floor(t.getMonth()/3)*3;return{from:new Date(t.getFullYear(),qStart,1),to:new Date(t)};}
  return{from:null,to:null};
};

/* ====== ICONS ====== */
const IC = {
  home:<svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="M3 9l9-7 9 7v11a2 2 0 01-2 2H5a2 2 0 01-2-2z"/><polyline points="9 22 9 12 15 12 15 22"/></svg>,
  list:<svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><line x1="8" y1="6" x2="21" y2="6"/><line x1="8" y1="12" x2="21" y2="12"/><line x1="8" y1="18" x2="21" y2="18"/><line x1="3" y1="6" x2="3.01" y2="6"/><line x1="3" y1="12" x2="3.01" y2="12"/><line x1="3" y1="18" x2="3.01" y2="18"/></svg>,
  chart:<svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><line x1="18" y1="20" x2="18" y2="10"/><line x1="12" y1="20" x2="12" y2="4"/><line x1="6" y1="20" x2="6" y2="14"/></svg>,
  settings:<svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><circle cx="12" cy="12" r="3"/><path d="M19.4 15a1.65 1.65 0 00.33 1.82l.06.06a2 2 0 010 2.83 2 2 0 01-2.83 0l-.06-.06a1.65 1.65 0 00-1.82-.33 1.65 1.65 0 00-1 1.51V21a2 2 0 01-4 0v-.09A1.65 1.65 0 009 19.4a1.65 1.65 0 00-1.82.33l-.06.06a2 2 0 01-2.83-2.83l.06-.06A1.65 1.65 0 004.68 15a1.65 1.65 0 00-1.51-1H3a2 2 0 010-4h.09A1.65 1.65 0 004.6 9a1.65 1.65 0 00-.33-1.82l-.06-.06a2 2 0 012.83-2.83l.06.06A1.65 1.65 0 009 4.68a1.65 1.65 0 001-1.51V3a2 2 0 014 0v.09a1.65 1.65 0 001 1.51 1.65 1.65 0 001.82-.33l.06-.06a2 2 0 012.83 2.83l-.06.06A1.65 1.65 0 0019.4 9a1.65 1.65 0 001.51 1H21a2 2 0 010 4h-.09a1.65 1.65 0 00-1.51 1z"/></svg>,
  plus:<svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round"><line x1="12" y1="5" x2="12" y2="19"/><line x1="5" y1="12" x2="19" y2="12"/></svg>,
  lock:<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><rect x="3" y="11" width="18" height="11" rx="2" ry="2"/><path d="M7 11V7a5 5 0 0110 0v4"/></svg>,
  search:<svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><circle cx="11" cy="11" r="8"/><line x1="21" y1="21" x2="16.65" y2="16.65"/></svg>,
  x:<svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round"><line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/></svg>,
  cal:<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><rect x="3" y="4" width="18" height="18" rx="2" ry="2"/><line x1="16" y1="2" x2="16" y2="6"/><line x1="8" y1="2" x2="8" y2="6"/><line x1="3" y1="10" x2="21" y2="10"/></svg>,
  chevDown:<svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round"><polyline points="6 9 12 15 18 9"/></svg>,
  chevL:<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><polyline points="15 18 9 12 15 6"/></svg>,
  chevR:<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><polyline points="9 18 15 12 9 6"/></svg>,
  edit:<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="M11 4H4a2 2 0 00-2 2v14a2 2 0 002 2h14a2 2 0 002-2v-7"/><path d="M18.5 2.5a2.121 2.121 0 013 3L12 15l-4 1 1-4 9.5-9.5z"/></svg>,
  trash:<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><polyline points="3 6 5 6 21 6"/><path d="M19 6v14a2 2 0 01-2 2H7a2 2 0 01-2-2V6m3 0V4a2 2 0 012-2h4a2 2 0 012 2v2"/></svg>,
  copy:<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><rect x="9" y="9" width="13" height="13" rx="2" ry="2"/><path d="M5 15H4a2 2 0 01-2-2V4a2 2 0 012-2h9a2 2 0 012 2v1"/></svg>,
  cash:<svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><rect x="1" y="4" width="22" height="16" rx="2"/><circle cx="12" cy="12" r="4"/></svg>,
  card:<svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><rect x="1" y="4" width="22" height="16" rx="2" ry="2"/><line x1="1" y1="10" x2="23" y2="10"/></svg>,
  arrowUp:<svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round"><line x1="12" y1="19" x2="12" y2="5"/><polyline points="5 12 12 5 19 12"/></svg>,
  arrowDown:<svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round"><line x1="12" y1="5" x2="12" y2="19"/><polyline points="19 12 12 19 5 12"/></svg>,
  undo:<svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><polyline points="1 4 1 10 7 10"/><path d="M3.51 15a9 9 0 102.13-9.36L1 10"/></svg>,
  check:<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round"><polyline points="20 6 9 17 4 12"/></svg>,
  close:<svg width="17" height="17" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round"><line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/></svg>,
  calc:<svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><rect x="4" y="2" width="16" height="20" rx="2"/><line x1="8" y1="6" x2="16" y2="6"/></svg>,
  bksp:<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="M21 4H8l-7 8 7 8h13a2 2 0 002-2V6a2 2 0 00-2-2z"/><line x1="18" y1="9" x2="12" y2="15"/><line x1="12" y1="9" x2="18" y2="15"/></svg>,
  sliders:<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><line x1="4" y1="21" x2="4" y2="14"/><line x1="4" y1="10" x2="4" y2="3"/><line x1="12" y1="21" x2="12" y2="12"/><line x1="12" y1="8" x2="12" y2="3"/><line x1="20" y1="21" x2="20" y2="16"/><line x1="20" y1="12" x2="20" y2="3"/><line x1="1" y1="14" x2="7" y2="14"/><line x1="9" y1="8" x2="15" y2="8"/><line x1="17" y1="16" x2="23" y2="16"/></svg>,
};

/* ====== ANIMATED NUMBER ====== */
function ANum({value, color, size=13}){
  const [disp,setDisp]=useState(value);
  const prev=useRef(value);
  useEffect(()=>{
    if(prev.current===value)return;
    const start=prev.current,diff=value-start,steps=24;let s=0;
    const iv=setInterval(()=>{s++;const p=1-Math.pow(1-s/steps,3);setDisp(Math.round(start+diff*p));if(s>=steps){clearInterval(iv);setDisp(value);prev.current=value;}},16);
    return()=>clearInterval(iv);
  },[value]);
  const abs=Math.abs(disp);
  const sign=disp>0?"+":disp<0?"−":"";
  return <span style={{fontFamily:"'JetBrains Mono',monospace",fontWeight:700,fontSize:size,color,letterSpacing:"-0.5px"}}>{sign}{abs.toLocaleString("ru-RU")} ₽</span>;
}

/* ====== HIGHLIGHT TEXT ====== */
function Highlight({text, query}){
  if(!query||!text) return <span>{text}</span>;
  const idx=text.toLowerCase().indexOf(query.toLowerCase());
  if(idx===-1) return <span>{text}</span>;
  return <span>{text.slice(0,idx)}<mark style={{background:"#fef08a",borderRadius:2,padding:"0 1px"}}>{text.slice(idx,idx+query.length)}</mark>{text.slice(idx+query.length)}</span>;
}

/* ====== SKELETON ====== */
function SkeletonRow(){
  return <div style={{display:"flex",alignItems:"center",padding:"12px 20px",gap:12,borderBottom:"1px solid #f8f9fb"}}>
    <div className="sk" style={{width:8,height:8,borderRadius:"50%",flexShrink:0}}/>
    <div className="sk" style={{flex:1,height:12,borderRadius:6,maxWidth:160}}/>
    <div className="sk" style={{width:80,height:12,borderRadius:6,marginLeft:"auto"}}/>
  </div>;
}
function SkeletonGroup(){
  return <div>
    <div style={{display:"flex",justifyContent:"space-between",padding:"10px 20px 7px",background:"#f8f9fb",borderBottom:"1px solid #f0f2f5"}}>
      <div className="sk" style={{width:60,height:11,borderRadius:4}}/>
      <div className="sk" style={{width:50,height:11,borderRadius:4}}/>
    </div>
    {[1,2,3].map(i=><SkeletonRow key={i}/>)}
  </div>;
}

/* ====== TOOLTIP ====== */
function Tip({children,text}){
  const [s,setS]=useState(false);const [p,setP]=useState({t:0,l:0});const r=useRef(null);
  return <div ref={r} onMouseEnter={()=>{if(r.current){const b=r.current.getBoundingClientRect();setP({t:b.top+b.height/2,l:b.right+10});}setS(true);}} onMouseLeave={()=>setS(false)} style={{position:"relative"}}>
    {children}
    {s&&<div style={{position:"fixed",top:p.t,left:p.l,transform:"translateY(-50%)",background:"#1a1d23",color:"#fff",fontSize:11,fontWeight:500,padding:"5px 10px",borderRadius:7,whiteSpace:"nowrap",zIndex:999,pointerEvents:"none",animation:"tipIn .15s ease"}}>{text}</div>}
  </div>;
}

/* ====== TOASTS ====== */
let tid=0;
function Toasts({toasts,onDismiss,onUndo}){return <div style={{position:"fixed",bottom:24,left:"50%",transform:"translateX(-50%)",zIndex:300,display:"flex",flexDirection:"column-reverse",gap:8,pointerEvents:"none"}}>{toasts.map(t=><TOne key={t.id} t={t} onD={onDismiss} onU={onUndo}/>)}</div>;}
function TOne({t,onD,onU}){
  const [pr,setPr]=useState(100);const [out,setOut]=useState(false);const iv=useRef(null);
  useEffect(()=>{iv.current=setInterval(()=>{setPr(p=>{const n=p-.6;if(n<=0){clearInterval(iv.current);setOut(true);setTimeout(()=>onD(t.id),350);return 0;}return n;});},30);return()=>clearInterval(iv.current);},[t.id,onD]);
  const doU=()=>{clearInterval(iv.current);setOut(true);onU(t);setTimeout(()=>onD(t.id),350);};
  const cl={delete:{bg:"#fef2f2",bd:"#fecaca",br:"#ef4444"},edit:{bg:"#eff6ff",bd:"#bfdbfe",br:"#3b82f6"},add:{bg:"#f0fdf4",bd:"#bbf7d0",br:"#22c55e"}};const c=cl[t.action]||cl.add;
  return <div style={{background:c.bg,border:`1px solid ${c.bd}`,borderRadius:14,padding:"12px 16px",minWidth:340,maxWidth:420,pointerEvents:"auto",boxShadow:"0 12px 40px rgba(0,0,0,.1)",animation:out?"toastOut .35s ease forwards":"toastIn .4s cubic-bezier(.34,1.56,.64,1)",position:"relative",overflow:"hidden"}}>
    <div style={{position:"absolute",bottom:0,left:0,height:3,background:c.br,width:`${pr}%`,transition:"width 30ms linear",opacity:.5}}/>
    <div style={{display:"flex",alignItems:"center",gap:10}}>
      <div style={{width:28,height:28,borderRadius:8,background:"#fff",display:"flex",alignItems:"center",justifyContent:"center",color:c.br,flexShrink:0}}>{t.action==="delete"?IC.trash:t.action==="edit"?IC.edit:IC.check}</div>
      <div style={{flex:1,minWidth:0}}><p style={{fontSize:13,fontWeight:600,color:"#1a1d23"}}>{t.title}</p><p style={{fontSize:11,color:"#667085",overflow:"hidden",textOverflow:"ellipsis",whiteSpace:"nowrap"}}>{t.message}</p></div>
      {t.undoData&&<button onClick={doU} style={{display:"flex",alignItems:"center",gap:4,padding:"6px 12px",borderRadius:7,border:"none",background:"#fff",fontSize:11,fontWeight:600,color:c.br,cursor:"pointer",flexShrink:0,boxShadow:"0 1px 3px rgba(0,0,0,.08)"}}><span style={{display:"flex"}}>{IC.undo}</span>Отменить</button>}
    </div>
  </div>;
}

/* ====== CONTEXT MENU ====== */
function CtxMenu({x,y,onEdit,onDup,onDel,onClose}){
  const r=useRef(null);
  useEffect(()=>{const h=e=>{if(r.current&&!r.current.contains(e.target))onClose();};document.addEventListener("mousedown",h);return()=>document.removeEventListener("mousedown",h);},[onClose]);
  const mi=(ic,lb,cl,fn)=><button key={lb} onClick={()=>{fn();onClose();}} style={{display:"flex",alignItems:"center",gap:9,width:"100%",border:"none",background:"none",padding:"8px 12px",fontSize:12,fontWeight:450,color:cl||"#344054",cursor:"pointer",borderRadius:6,fontFamily:"inherit",transition:"background .15s"}} onMouseEnter={e=>e.currentTarget.style.background="#f8f9fb"} onMouseLeave={e=>e.currentTarget.style.background="none"}><span style={{display:"flex",color:cl||"#667085"}}>{ic}</span>{lb}</button>;
  const ax=x+180>window.innerWidth?x-180:x, ay=y+120>window.innerHeight?y-120:y;
  return <div ref={r} style={{position:"fixed",top:ay,left:ax,background:"#fff",borderRadius:11,border:"1px solid #e8eaed",padding:4,minWidth:175,zIndex:200,boxShadow:"0 12px 40px rgba(0,0,0,.15)",animation:"scaleIn .12s cubic-bezier(.34,1.56,.64,1)"}}>
    {mi(IC.edit,"Редактировать",null,onEdit)}{mi(IC.copy,"Дублировать",null,onDup)}
    <div style={{height:1,background:"#f0f2f5",margin:"3px 0"}}/>{mi(IC.trash,"Удалить","#ef4444",onDel)}
  </div>;
}

/* ====== CALENDAR ====== */
function CalPick({sel,onSel,onClose}){
  const r=useRef(null);
  const mo=["Январь","Февраль","Март","Апрель","Май","Июнь","Июль","Август","Сентябрь","Октябрь","Ноябрь","Декабрь"];
  const wd=["Пн","Вт","Ср","Чт","Пт","Сб","Вс"];
  const base=sel instanceof Date?sel:new Date();
  const [vm,setVm]=useState(base.getMonth());
  const [vy,setVy]=useState(base.getFullYear());
  useEffect(()=>{const h=e=>{if(r.current&&!r.current.contains(e.target))onClose();};document.addEventListener("mousedown",h);return()=>document.removeEventListener("mousedown",h);},[onClose]);
  let sd=new Date(vy,vm,1).getDay()-1;if(sd<0)sd=6;const dim=new Date(vy,vm+1,0).getDate();
  const cells=[];for(let i=0;i<sd;i++)cells.push(null);for(let d=1;d<=dim;d++)cells.push(d);
  const selD=sel instanceof Date?sel:null;
  const isSel=d=>selD&&d===selD.getDate()&&vm===selD.getMonth()&&vy===selD.getFullYear();
  const isNow=d=>{const t=new Date();return d===t.getDate()&&vm===t.getMonth()&&vy===t.getFullYear();};
  return <div ref={r} style={{position:"absolute",top:"100%",left:0,marginTop:4,background:"#fff",borderRadius:14,border:"1px solid #e8eaed",padding:14,zIndex:60,width:275,boxShadow:"0 12px 40px rgba(0,0,0,.12)",animation:"scaleIn .18s cubic-bezier(.34,1.56,.64,1)"}}>
    <div style={{display:"flex",justifyContent:"space-between",alignItems:"center",marginBottom:10}}>
      <button onClick={()=>{if(vm===0){setVm(11);setVy(y=>y-1);}else setVm(m=>m-1);}} style={{border:"none",background:"none",cursor:"pointer",padding:3,display:"flex",color:"#667085"}}>{IC.chevL}</button>
      <span style={{fontSize:13,fontWeight:600}}>{mo[vm]} {vy}</span>
      <button onClick={()=>{if(vm===11){setVm(0);setVy(y=>y+1);}else setVm(m=>m+1);}} style={{border:"none",background:"none",cursor:"pointer",padding:3,display:"flex",color:"#667085"}}>{IC.chevR}</button>
    </div>
    <div style={{display:"grid",gridTemplateColumns:"repeat(7,1fr)",gap:2,textAlign:"center"}}>
      {wd.map(w=><div key={w} style={{fontSize:10,color:"#98a2b3",fontWeight:500,padding:"3px 0"}}>{w}</div>)}
      {cells.map((d,i)=><div key={i} onClick={()=>d&&(()=>{onSel(new Date(vy,vm,d));onClose();})()} style={{width:32,height:32,display:"flex",alignItems:"center",justifyContent:"center",fontSize:12,borderRadius:8,cursor:d?"pointer":"default",fontWeight:isSel(d)?600:isNow(d)?500:400,background:isSel(d)?"#1a1d23":"transparent",color:isSel(d)?"#fff":isNow(d)?"#3b82f6":d?"#344054":"transparent",transition:"all .15s"}} onMouseEnter={e=>{if(d&&!isSel(d))e.currentTarget.style.background="#f0f2f5";}} onMouseLeave={e=>{if(d&&!isSel(d))e.currentTarget.style.background="transparent";}}>{d||""}</div>)}
    </div>
  </div>;
}

/* ====== DATE RANGE with PRESETS ====== */
function DateRangeFilter({from,to,onFromChange,onToChange}){
  const [open,setOpen]=useState(false);
  const [showFrom,setShowFrom]=useState(false);
  const [showTo,setShowTo]=useState(false);
  const ref=useRef(null);
  useEffect(()=>{const h=e=>{if(ref.current&&!ref.current.contains(e.target)){setOpen(false);setShowFrom(false);setShowTo(false);}};document.addEventListener("mousedown",h);return()=>document.removeEventListener("mousedown",h);},[]);

  const presets=[
    {k:"today",l:"Сегодня"},{k:"week",l:"Эта неделя"},{k:"month",l:"Этот месяц"},
    {k:"prev_month",l:"Прошлый месяц"},{k:"quarter",l:"Квартал"},
  ];
  const active=from||to;
  const label=from&&to?`${fmtD(from)} — ${fmtD(to)}`:from?`от ${fmtD(from)}`:to?`до ${fmtD(to)}`:"Период";

  return <div ref={ref} style={{position:"relative"}}>
    <button onClick={()=>{setOpen(p=>!p);setShowFrom(false);setShowTo(false);}} style={{display:"flex",alignItems:"center",gap:6,padding:"7px 12px",borderRadius:9,background:active?"#eff6ff":"#f8f9fb",border:active?"1px solid #bfdbfe":"1px solid #e8eaed",fontSize:12,color:active?"#3b82f6":"#667085",fontWeight:active?600:400,cursor:"pointer",fontFamily:"inherit",transition:"all .2s",whiteSpace:"nowrap"}}>
      <span style={{display:"flex",color:active?"#3b82f6":"#98a2b3"}}>{IC.cal}</span>
      {label}
      {active&&<span onClick={e=>{e.stopPropagation();onFromChange(null);onToChange(null);setOpen(false);}} style={{display:"flex",color:"#98a2b3",marginLeft:2}} onMouseEnter={e=>e.currentTarget.style.color="#ef4444"} onMouseLeave={e=>e.currentTarget.style.color="#98a2b3"}>{IC.x}</span>}
      <span style={{display:"flex",color:"#98a2b3",transform:open?"rotate(180deg)":"rotate(0deg)",transition:"transform .2s"}}>{IC.chevDown}</span>
    </button>

    {open&&<div style={{position:"absolute",top:"calc(100% + 6px)",left:0,background:"#fff",borderRadius:14,border:"1px solid #e8eaed",padding:12,zIndex:60,minWidth:300,boxShadow:"0 12px 40px rgba(0,0,0,.12)",animation:"scaleIn .18s cubic-bezier(.34,1.56,.64,1)"}}>
      {/* Presets */}
      <p style={{fontSize:10,fontWeight:600,color:"#98a2b3",textTransform:"uppercase",letterSpacing:".5px",marginBottom:8}}>Быстрый выбор</p>
      <div style={{display:"flex",flexWrap:"wrap",gap:5,marginBottom:14}}>
        {presets.map(p=>{
          const r=getPresetRange(p.k);
          const isActive=from&&to&&fmtD(from)===fmtD(r.from)&&fmtD(to)===fmtD(r.to);
          return <button key={p.k} onClick={()=>{onFromChange(r.from);onToChange(r.to);setOpen(false);}} style={{padding:"5px 11px",borderRadius:8,border:"none",fontSize:11,fontWeight:isActive?600:400,color:isActive?"#fff":"#344054",background:isActive?"#1a1d23":"#f0f2f5",cursor:"pointer",fontFamily:"inherit",transition:"all .15s"}} onMouseEnter={e=>{if(!isActive)e.currentTarget.style.background="#e8eaed";}} onMouseLeave={e=>{if(!isActive)e.currentTarget.style.background="#f0f2f5";}}>{p.l}</button>;
        })}
      </div>
      <div style={{height:1,background:"#f0f2f5",marginBottom:12}}/>
      {/* Custom range */}
      <p style={{fontSize:10,fontWeight:600,color:"#98a2b3",textTransform:"uppercase",letterSpacing:".5px",marginBottom:8}}>Произвольный период</p>
      <div style={{display:"flex",gap:8,alignItems:"center"}}>
        <div style={{position:"relative",flex:1}}>
          <button onClick={()=>{setShowFrom(p=>!p);setShowTo(false);}} style={{width:"100%",display:"flex",alignItems:"center",gap:6,padding:"7px 10px",borderRadius:8,background:"#f8f9fb",border:showFrom?"1px solid #3b82f6":"1px solid #e8eaed",fontSize:12,color:from?"#344054":"#98a2b3",cursor:"pointer",fontFamily:"inherit"}}>
            {from?fmtD(from):"Дата от"}
          </button>
          {showFrom&&<CalPick sel={from||new Date()} onSel={d=>{onFromChange(d);setShowFrom(false);}} onClose={()=>setShowFrom(false)}/>}
        </div>
        <span style={{color:"#d0d5dd",fontSize:12,flexShrink:0}}>—</span>
        <div style={{position:"relative",flex:1}}>
          <button onClick={()=>{setShowTo(p=>!p);setShowFrom(false);}} style={{width:"100%",display:"flex",alignItems:"center",gap:6,padding:"7px 10px",borderRadius:8,background:"#f8f9fb",border:showTo?"1px solid #3b82f6":"1px solid #e8eaed",fontSize:12,color:to?"#344054":"#98a2b3",cursor:"pointer",fontFamily:"inherit"}}>
            {to?fmtD(to):"Дата до"}
          </button>
          {showTo&&<CalPick sel={to||new Date()} onSel={d=>{onToChange(d);setShowTo(false);}} onClose={()=>setShowTo(false)}/>}
        </div>
      </div>
    </div>}
  </div>;
}

/* ====== AMOUNT RANGE FILTER ====== */
function AmountFilter({min,max,onMin,onMax,absMax}){
  const [open,setOpen]=useState(false);
  const ref=useRef(null);
  useEffect(()=>{const h=e=>{if(ref.current&&!ref.current.contains(e.target))setOpen(false);};document.addEventListener("mousedown",h);return()=>document.removeEventListener("mousedown",h);},[]);
  const active=min>0||max<absMax;
  const pct=v=>Math.round((v/absMax)*100);

  return <div ref={ref} style={{position:"relative"}}>
    <button onClick={()=>setOpen(p=>!p)} style={{display:"flex",alignItems:"center",gap:6,padding:"7px 12px",borderRadius:9,background:active?"#f5f3ff":"#f8f9fb",border:active?"1px solid #c4b5fd":"1px solid #e8eaed",fontSize:12,color:active?"#7c3aed":"#667085",fontWeight:active?600:400,cursor:"pointer",fontFamily:"inherit",transition:"all .2s",whiteSpace:"nowrap"}}>
      <span style={{display:"flex",color:active?"#7c3aed":"#98a2b3"}}>{IC.sliders}</span>
      {active?`${min.toLocaleString("ru-RU")} — ${max.toLocaleString("ru-RU")} ₽`:"Сумма"}
      {active&&<span onClick={e=>{e.stopPropagation();onMin(0);onMax(absMax);setOpen(false);}} style={{display:"flex",color:"#98a2b3",marginLeft:2}} onMouseEnter={e=>e.currentTarget.style.color="#ef4444"} onMouseLeave={e=>e.currentTarget.style.color="#98a2b3"}>{IC.x}</span>}
    </button>

    {open&&<div style={{position:"absolute",top:"calc(100% + 6px)",left:0,background:"#fff",borderRadius:14,border:"1px solid #e8eaed",padding:16,zIndex:60,width:280,boxShadow:"0 12px 40px rgba(0,0,0,.12)",animation:"scaleIn .18s cubic-bezier(.34,1.56,.64,1)"}}>
      <p style={{fontSize:11,fontWeight:600,color:"#344054",marginBottom:14}}>Диапазон суммы</p>
      
      {/* Track visual */}
      <div style={{position:"relative",height:4,background:"#f0f2f5",borderRadius:2,marginBottom:18,marginTop:4}}>
        <div style={{position:"absolute",left:`${pct(min)}%`,right:`${100-pct(max)}%`,top:0,height:"100%",background:"#7c3aed",borderRadius:2}}/>
        {/* Min thumb */}
        <input type="range" min={0} max={absMax} step={1000} value={min} onChange={e=>{const v=+e.target.value;if(v<=max)onMin(v);}}
          style={{position:"absolute",top:-6,left:0,width:"100%",appearance:"none",background:"transparent",pointerEvents:"none"}}
          className="range-thumb-l"/>
        {/* Max thumb */}
        <input type="range" min={0} max={absMax} step={1000} value={max} onChange={e=>{const v=+e.target.value;if(v>=min)onMax(v);}}
          style={{position:"absolute",top:-6,left:0,width:"100%",appearance:"none",background:"transparent",pointerEvents:"none"}}
          className="range-thumb-r"/>
      </div>
      
      <div style={{display:"flex",gap:8}}>
        <div style={{flex:1}}>
          <label style={{fontSize:10,color:"#98a2b3",display:"block",marginBottom:4}}>От</label>
          <input type="number" value={min} onChange={e=>onMin(Math.min(+e.target.value,max))} style={{width:"100%",padding:"6px 10px",borderRadius:8,border:"1px solid #e8eaed",fontSize:12,color:"#344054",fontFamily:"'JetBrains Mono',monospace",outline:"none"}} onFocus={e=>e.target.style.borderColor="#7c3aed"} onBlur={e=>e.target.style.borderColor="#e8eaed"}/>
        </div>
        <div style={{flex:1}}>
          <label style={{fontSize:10,color:"#98a2b3",display:"block",marginBottom:4}}>До</label>
          <input type="number" value={max} onChange={e=>onMax(Math.max(+e.target.value,min))} style={{width:"100%",padding:"6px 10px",borderRadius:8,border:"1px solid #e8eaed",fontSize:12,color:"#344054",fontFamily:"'JetBrains Mono',monospace",outline:"none"}} onFocus={e=>e.target.style.borderColor="#7c3aed"} onBlur={e=>e.target.style.borderColor="#e8eaed"}/>
        </div>
      </div>
    </div>}
  </div>;
}

/* ====== CATEGORY COMBOBOX ====== */
function CatComboBox({value, onChange, catNames, catColors}){
  const [open,setOpen]=useState(false);
  const [query,setQuery]=useState("");
  const ref=useRef(null);
  const inputRef=useRef(null);
  useEffect(()=>{const h=e=>{if(ref.current&&!ref.current.contains(e.target))setOpen(false);};document.addEventListener("mousedown",h);return()=>document.removeEventListener("mousedown",h);},[]);
  
  const filtered=query?catNames.filter(c=>c.toLowerCase().includes(query.toLowerCase())):catNames;
  const active=value!=="all";
  
  const selectVal=(v)=>{onChange(v);setQuery("");setOpen(false);};

  return <div ref={ref} style={{position:"relative",minWidth:160}}>
    <div onClick={()=>{setOpen(p=>!p);setTimeout(()=>inputRef.current?.focus(),50);}} style={{display:"flex",alignItems:"center",gap:6,padding:"7px 12px",borderRadius:9,background:active?"#f0fdf4":"#f8f9fb",border:active?"1px solid #bbf7d0":"1px solid #e8eaed",cursor:"pointer",transition:"all .2s",minHeight:34}}>
      {active&&<div style={{width:8,height:8,borderRadius:"50%",background:catColors[value]||"#94a3b8",flexShrink:0}}/>}
      {open
        ? <input ref={inputRef} value={query} onChange={e=>setQuery(e.target.value)} onClick={e=>e.stopPropagation()} placeholder="Поиск категории..." style={{border:"none",outline:"none",background:"transparent",fontSize:12,color:"#344054",fontFamily:"inherit",width:"100%",minWidth:80}} autoFocus/>
        : <span style={{fontSize:12,color:active?"#15803d":"#667085",fontWeight:active?600:400,flex:1,whiteSpace:"nowrap",overflow:"hidden",textOverflow:"ellipsis"}}>{active?value:"Категория"}</span>
      }
      {active&&!open&&<span onClick={e=>{e.stopPropagation();onChange("all");}} style={{display:"flex",color:"#98a2b3",flexShrink:0}} onMouseEnter={e=>e.currentTarget.style.color="#ef4444"} onMouseLeave={e=>e.currentTarget.style.color="#98a2b3"}>{IC.x}</span>}
      {!active&&<span style={{display:"flex",color:"#98a2b3",flexShrink:0,transform:open?"rotate(180deg)":"",transition:"transform .2s"}}>{IC.chevDown}</span>}
    </div>

    {open&&<div style={{position:"absolute",top:"calc(100%+4px)",left:0,marginTop:4,background:"#fff",borderRadius:12,border:"1px solid #e8eaed",zIndex:60,minWidth:200,boxShadow:"0 12px 40px rgba(0,0,0,.12)",animation:"scaleIn .15s cubic-bezier(.34,1.56,.64,1)",overflow:"hidden"}}>
      <div style={{maxHeight:220,overflowY:"auto"}}>
        <div onClick={()=>selectVal("all")} style={{display:"flex",alignItems:"center",gap:9,padding:"9px 14px",cursor:"pointer",fontSize:12,color:value==="all"?"#fff":"#667085",fontWeight:value==="all"?600:400,background:value==="all"?"#1a1d23":"transparent",transition:"background .12s"}} onMouseEnter={e=>{if(value!=="all")e.currentTarget.style.background="#f8f9fb";}} onMouseLeave={e=>{if(value!=="all")e.currentTarget.style.background="transparent";}}>
          <div style={{width:8,height:8,borderRadius:"50%",background:"#d0d5dd"}}/>
          Все категории
        </div>
        {filtered.length===0&&<div style={{padding:"12px 14px",fontSize:12,color:"#98a2b3",textAlign:"center"}}>Не найдено</div>}
        {filtered.map(c=><div key={c} onClick={()=>selectVal(c)} style={{display:"flex",alignItems:"center",gap:9,padding:"9px 14px",cursor:"pointer",fontSize:12,color:value===c?"#fff":"#344054",fontWeight:value===c?600:400,background:value===c?"#1a1d23":"transparent",transition:"background .12s"}} onMouseEnter={e=>{if(value!==c)e.currentTarget.style.background="#f8f9fb";}} onMouseLeave={e=>{if(value!==c)e.currentTarget.style.background="transparent";}}>
          <div style={{width:8,height:8,borderRadius:"50%",background:catColors[c]||"#94a3b8",flexShrink:0}}/>
          <span style={{flex:1}}>{c}</span>
          {value===c&&<span style={{display:"flex",color:"#fff"}}>{IC.check}</span>}
        </div>)}
      </div>
    </div>}
  </div>;
}

/* ====== WALLET TOGGLE BUTTON ====== */
function WalletToggle({value, onChange, wide=false}){
  const isCash = value === "cash";
  const w = wide ? 160 : 72;
  const pillW = wide ? 74 : 30;
  return (
    <button
      onClick={()=>onChange(isCash?"card":"cash")}
      title={isCash?"Наличные — нажмите для переключения":"Безналичные — нажмите для переключения"}
      style={{
        position:"relative",width:w,height:36,borderRadius:10,border:"none",
        background:isCash?"#f0fdf4":"#eff6ff",
        cursor:"pointer",flexShrink:0,overflow:"hidden",
        transition:"background .25s",padding:0,
      }}
    >
      {/* sliding pill */}
      <span style={{
        position:"absolute",top:3,
        left: isCash ? 3 : `calc(100% - ${pillW+3}px)`,
        width:pillW,height:30,borderRadius:7,
        background:isCash?"#22c55e":"#3b82f6",
        transition:"left .22s cubic-bezier(.34,1.56,.64,1)",
        display:"flex",alignItems:"center",justifyContent:"center",
        gap:5,color:"#fff",
        fontSize: wide ? 12 : 11,
        fontWeight: wide ? 600 : 500,
        fontFamily:"'DM Sans','Segoe UI',sans-serif",
      }}>
        {isCash ? IC.cash : IC.card}
        {wide && (isCash ? "Наличные" : "Безналичные")}
      </span>
      {/* label on the opposite side */}
      <span style={{
        position:"absolute",top:0,left:0,width:"100%",height:"100%",
        display:"flex",alignItems:"center",
        paddingLeft: isCash ? pillW+8 : 10,
        paddingRight: isCash ? 10 : pillW+8,
        fontSize:11,fontWeight:500,
        color:isCash?"#15803d":"#1d4ed8",
        transition:"all .22s",
        whiteSpace:"nowrap",
        justifyContent: isCash ? "flex-start" : "flex-end",
        fontFamily:"'DM Sans','Segoe UI',sans-serif",
        pointerEvents:"none",
      }}>
        {wide ? (isCash ? "Безналичные →" : "← Наличные") : (isCash?"Безнал":"Нал")}
      </span>
    </button>
  );
}

/* ====== INLINE ADD ROW ====== */
function InlineAddRow({date,cats,catColors,onAdd,onClose}){
  const [type,setType]=useState("expense");
  const [cat,setCat]=useState("");
  const [amt,setAmt]=useState("");
  const [desc,setDesc]=useState("");
  const [wallet,setWallet]=useState("cash");
  const [catOpen,setCatOpen]=useState(false);
  const catRef=useRef(null);
  const amtRef=useRef(null);

  useEffect(()=>{amtRef.current?.focus();},[]);
  useEffect(()=>{const h=e=>{if(catRef.current&&!catRef.current.contains(e.target))setCatOpen(false);};document.addEventListener("mousedown",h);return()=>document.removeEventListener("mousedown",h);},[]);

  const submit=()=>{const a=parseFloat(amt)||0;if(a<=0||!cat)return;onAdd({id:Date.now(),date,type,cat,amount:type==="expense"?-a:a,wallet,desc});onClose();};
  const allCatNames=cats.map(c=>c.name);

  return (
    <div style={{background:"#f8f9fb",borderBottom:"1px solid #e8eaed",animation:"rowIn .18s ease"}}>
      {/* Main row */}
      <div style={{display:"flex",alignItems:"center",gap:8,padding:"10px 20px 6px"}}>
        {/* Type toggle */}
        <div style={{display:"flex",background:"#f0f2f5",borderRadius:8,padding:2,flexShrink:0}}>
          {[{v:"expense",l:"−"},{v:"income",l:"+"}].map(t=><button key={t.v} onClick={()=>setType(t.v)} style={{width:26,height:26,borderRadius:6,border:"none",fontSize:15,fontWeight:700,cursor:"pointer",fontFamily:"inherit",color:type===t.v?"#fff":"#98a2b3",background:type===t.v?(t.v==="expense"?"#ef4444":"#22c55e"):"transparent",transition:"all .15s"}}>{t.l}</button>)}
        </div>
        {/* Cat combobox inline */}
        <div ref={catRef} style={{position:"relative",flex:"0 0 150px"}}>
          <div onClick={()=>setCatOpen(p=>!p)} style={{display:"flex",alignItems:"center",gap:6,padding:"6px 10px",borderRadius:8,background:"#fff",border:catOpen?"1px solid #3b82f6":"1px solid #e8eaed",cursor:"pointer",fontSize:12,color:cat?"#344054":"#98a2b3"}}>
            {cat&&<div style={{width:7,height:7,borderRadius:"50%",background:catColors[cat]||"#94a3b8",flexShrink:0}}/>}
            <span style={{flex:1,whiteSpace:"nowrap",overflow:"hidden",textOverflow:"ellipsis"}}>{cat||"Категория *"}</span>
            <span style={{display:"flex",color:"#d0d5dd"}}>{IC.chevDown}</span>
          </div>
          {catOpen&&<div style={{position:"absolute",bottom:"calc(100% + 4px)",left:0,background:"#fff",borderRadius:10,border:"1px solid #e8eaed",minWidth:170,zIndex:70,boxShadow:"0 8px 24px rgba(0,0,0,.12)",animation:"scaleIn .15s ease",overflow:"hidden"}}>
            <div style={{maxHeight:180,overflowY:"auto"}}>
              {allCatNames.map(c=><div key={c} onClick={()=>{setCat(c);setCatOpen(false);}} style={{display:"flex",alignItems:"center",gap:8,padding:"8px 12px",cursor:"pointer",fontSize:12,color:"#344054",transition:"background .1s"}} onMouseEnter={e=>e.currentTarget.style.background="#f8f9fb"} onMouseLeave={e=>e.currentTarget.style.background="transparent"}>
                <div style={{width:7,height:7,borderRadius:"50%",background:catColors[c]||"#94a3b8"}}/>
                {c}
              </div>)}
            </div>
          </div>}
        </div>
        {/* Amount */}
        <input ref={amtRef} type="text" placeholder="Сумма *" value={amt} onChange={e=>setAmt(e.target.value)} onKeyDown={e=>{if(e.key==="Enter")submit();if(e.key==="Escape")onClose();}} style={{flex:"0 0 120px",padding:"6px 10px",borderRadius:8,border:"1px solid #e8eaed",fontSize:12,fontFamily:"'JetBrains Mono',monospace",outline:"none",color:"#344054"}} onFocus={e=>e.target.style.borderColor="#3b82f6"} onBlur={e=>e.target.style.borderColor="#e8eaed"}/>
        {/* Wallet — single toggle button */}
        <WalletToggle value={wallet} onChange={setWallet}/>
        {/* Actions */}
        <button onClick={submit} style={{width:30,height:30,borderRadius:8,border:"none",background:"#22c55e",color:"#fff",display:"flex",alignItems:"center",justifyContent:"center",cursor:"pointer",flexShrink:0,marginLeft:"auto"}}>{IC.check}</button>
        <button onClick={onClose} style={{width:30,height:30,borderRadius:8,border:"none",background:"#f0f2f5",color:"#667085",display:"flex",alignItems:"center",justifyContent:"center",cursor:"pointer",flexShrink:0}}>{IC.x}</button>
      </div>
      {/* Description row */}
      <div style={{padding:"0 20px 10px",paddingLeft:72}}>
        <input
          type="text"
          placeholder="Описание (необязательно)"
          value={desc}
          onChange={e=>setDesc(e.target.value)}
          onKeyDown={e=>{if(e.key==="Enter")submit();if(e.key==="Escape")onClose();}}
          style={{width:"100%",padding:"5px 10px",borderRadius:7,border:"1px solid #e8eaed",fontSize:11,fontFamily:"inherit",outline:"none",color:"#667085",background:"#fff"}}
          onFocus={e=>e.target.style.borderColor="#3b82f6"}
          onBlur={e=>e.target.style.borderColor="#e8eaed"}
        />
      </div>
    </div>
  );
}

/* ====== CALC ====== */
function Calc({value,onChange,onClose}){
  const [d,setD]=useState(value||"0");const [pv,setPv]=useState(null);const [op,setOp]=useState(null);const [fr,setFr]=useState(true);
  const num=n=>{if(fr){setD(String(n));setFr(false);}else setD(x=>x==="0"?String(n):x+n);};
  const dot=()=>{if(fr){setD("0.");setFr(false);return;}if(!d.includes("."))setD(x=>x+".");};
  const doOp=o=>{const c=parseFloat(d)||0;if(pv!==null&&op&&!fr){const r=ev(pv,c,op);setD(String(r));setPv(r);}else setPv(c);setOp(o);setFr(true);};
  const ev=(a,b,o)=>{if(o==="+")return a+b;if(o==="−")return a-b;if(o==="×")return a*b;if(o==="÷"&&b)return a/b;return b;};
  const eq=()=>{if(pv!==null&&op){const r=ev(pv,parseFloat(d)||0,op);setD(String(r));setPv(null);setOp(null);setFr(true);}};
  const clr=()=>{setD("0");setPv(null);setOp(null);setFr(true);};const bk=()=>setD(x=>x.length>1?x.slice(0,-1):"0");
  const done=()=>{let f;if(pv!==null&&op)f=ev(pv,parseFloat(d)||0,op);else f=parseFloat(d)||0;onChange(String(Math.round(f*100)/100));onClose();};
  const B=(l,a,s={})=><button onClick={a} style={{width:"100%",height:40,border:"none",borderRadius:9,fontSize:15,fontWeight:500,fontFamily:"'JetBrains Mono',monospace",cursor:"pointer",background:"#f0f2f5",color:"#1a1d23",...s}}>{l}</button>;
  return <div style={{marginTop:10,background:"#fff",borderRadius:14,border:"1px solid #e8eaed",padding:14,animation:"scaleIn .2s ease",boxShadow:"0 8px 32px rgba(0,0,0,.08)"}}>
    <div style={{background:"#f8f9fb",borderRadius:9,padding:"10px 14px",marginBottom:10,fontFamily:"'JetBrains Mono',monospace",fontSize:20,fontWeight:600,textAlign:"right",color:"#1a1d23"}}>{op&&<span style={{fontSize:11,color:"#98a2b3",marginRight:6}}>{pv} {op}</span>}{d}</div>
    <div style={{display:"grid",gridTemplateColumns:"1fr 1fr 1fr 1fr",gap:5}}>
      {B("C",clr,{background:"#fef2f2",color:"#ef4444"})}{B(<span style={{display:"flex",justifyContent:"center"}}>{IC.bksp}</span>,bk)}{B("÷",()=>doOp("÷"),{background:"#eff6ff",color:"#3b82f6"})}{B("×",()=>doOp("×"),{background:"#eff6ff",color:"#3b82f6"})}
      {[7,8,9].map(n=><span key={n}>{B(n,()=>num(n))}</span>)}{B("−",()=>doOp("−"),{background:"#eff6ff",color:"#3b82f6"})}
      {[4,5,6].map(n=><span key={n}>{B(n,()=>num(n))}</span>)}{B("+",()=>doOp("+"),{background:"#eff6ff",color:"#3b82f6"})}
      {[1,2,3].map(n=><span key={n}>{B(n,()=>num(n))}</span>)}{B("=",eq,{background:"#1a1d23",color:"#fff"})}
      <span style={{gridColumn:"span 2"}}>{B(0,()=>num(0),{width:"100%"})}</span>{B(".",dot)}{B("✓",done,{background:"linear-gradient(135deg,#22c55e,#16a34a)",color:"#fff",fontSize:16})}
    </div>
  </div>;
}

/* ====== TX MODAL ====== */
function TxModal({cats,catColors,onAdd,onClose,editTx=null}){
  const [aType,setAType]=useState(editTx?.type||"expense");
  const [aAmt,setAAmt]=useState(editTx?String(Math.abs(editTx.amount)):"");
  const [aCat,setACat]=useState(editTx?.cat||"");
  const [aDesc,setADesc]=useState(editTx?.desc||"");
  const [aDate,setADate]=useState(editTx?(()=>{const[d,m,y]=editTx.date.split(".");return new Date(y,m-1,d);})():new Date());
  const [aWallet,setAWallet]=useState(editTx?.wallet||"cash");
  const [sCalc,setSCalc]=useState(false);
  const [sCal,setsCal]=useState(false);
  const [sAllCats,setSAllCats]=useState(false);
  const allCatNames=cats.map(c=>c.name);const topCats=allCatNames.slice(0,4);
  return <div style={{position:"fixed",inset:0,background:"rgba(0,0,0,.4)",backdropFilter:"blur(4px)",display:"flex",alignItems:"center",justifyContent:"center",zIndex:100,animation:"backdropIn .2s"}} onClick={e=>{if(e.target===e.currentTarget)onClose();}}>
    <div style={{background:"#fff",borderRadius:22,width:420,maxHeight:"90vh",overflowY:"auto",padding:28,animation:"modalIn .3s cubic-bezier(.34,1.56,.64,1)",boxShadow:"0 24px 80px rgba(0,0,0,.15)"}}>
      <div style={{display:"flex",justifyContent:"space-between",alignItems:"center",marginBottom:22}}>
        <h3 style={{fontSize:17,fontWeight:600}}>{editTx?"Редактировать операцию":"Новая операция"}</h3>
        <button onClick={onClose} style={{border:"none",background:"none",cursor:"pointer",color:"#98a2b3",display:"flex",padding:4,borderRadius:8}}>{IC.close}</button>
      </div>
      <div style={{display:"flex",background:"#f0f2f5",borderRadius:11,padding:3,marginBottom:18}}>
        {["expense","income"].map(t=><button key={t} onClick={()=>setAType(t)} style={{flex:1,padding:"9px",borderRadius:8,fontSize:13,fontWeight:500,color:aType===t?"#fff":"#667085",background:aType===t?(t==="expense"?"#ef4444":"#22c55e"):"transparent",border:"none",cursor:"pointer",fontFamily:"inherit",transition:"all .2s"}}>{t==="expense"?"Расход":"Доход"}</button>)}
      </div>
      <div style={{padding:18,background:"#f8f9fb",borderRadius:14,marginBottom:18}}>
        <div style={{display:"flex",alignItems:"center",gap:8}}>
          <input type="text" placeholder="0" value={aAmt} onChange={e=>setAAmt(e.target.value)} autoFocus style={{flex:1,fontFamily:"'JetBrains Mono',monospace",fontSize:30,fontWeight:600,border:"none",outline:"none",width:"100%",textAlign:"center",color:"#1a1d23",background:"transparent"}}/>
          <button onClick={()=>setSCalc(!sCalc)} style={{border:"none",borderRadius:9,width:38,height:38,background:sCalc?"#1a1d23":"#e8eaed",color:sCalc?"#fff":"#667085",display:"flex",alignItems:"center",justifyContent:"center",cursor:"pointer",flexShrink:0,transition:"all .2s"}}>{IC.calc}</button>
        </div>
        <p style={{textAlign:"center",fontSize:11,color:"#98a2b3",marginTop:5}}>Сумма в рублях</p>
        {sCalc&&<Calc value={aAmt} onChange={setAAmt} onClose={()=>setSCalc(false)}/>}
      </div>
      <div style={{marginBottom:18}}>
        <p style={{fontSize:11,fontWeight:500,color:"#98a2b3",marginBottom:8,textTransform:"uppercase",letterSpacing:".5px"}}>Касса</p>
        <WalletToggle value={aWallet} onChange={setAWallet} wide={true}/>
      </div>
      <div style={{marginBottom:18}}>
        <p style={{fontSize:11,fontWeight:500,color:"#98a2b3",marginBottom:8,textTransform:"uppercase",letterSpacing:".5px"}}>Категория</p>
        <div style={{display:"flex",flexWrap:"wrap",gap:7}}>
          {(sAllCats?allCatNames:topCats).map(c=><button key={c} onClick={()=>setACat(c)} style={{padding:"7px 14px",borderRadius:18,fontSize:12,fontWeight:450,color:aCat===c?"#fff":"#344054",background:aCat===c?"#1a1d23":"#f0f2f5",border:"none",cursor:"pointer",fontFamily:"inherit",transition:"all .15s"}}>{c}</button>)}
          {!sAllCats&&allCatNames.length>4&&<button onClick={()=>setSAllCats(true)} style={{padding:"7px 14px",borderRadius:18,fontSize:12,color:"#3b82f6",background:"#eff6ff",border:"none",cursor:"pointer",fontFamily:"inherit",display:"flex",alignItems:"center",gap:3}}>Ещё{IC.chevDown}</button>}
          {sAllCats&&<button onClick={()=>setSAllCats(false)} style={{padding:"7px 14px",borderRadius:18,fontSize:12,color:"#3b82f6",background:"#eff6ff",border:"none",cursor:"pointer",fontFamily:"inherit"}}>Свернуть</button>}
        </div>
      </div>
      <div style={{marginBottom:18}}>
        <p style={{fontSize:11,fontWeight:500,color:"#98a2b3",marginBottom:8,textTransform:"uppercase",letterSpacing:".5px"}}>Описание</p>
        <textarea value={aDesc} onChange={e=>setADesc(e.target.value)} placeholder="Комментарий..." rows={2} style={{width:"100%",padding:"10px 12px",borderRadius:11,border:"1px solid #e8eaed",background:"#f8f9fb",fontSize:13,fontFamily:"inherit",resize:"vertical",outline:"none",color:"#1a1d23",minHeight:50,lineHeight:1.5}} onFocus={e=>e.target.style.borderColor="#3b82f6"} onBlur={e=>e.target.style.borderColor="#e8eaed"}/>
      </div>
      <div style={{marginBottom:24,position:"relative"}}>
        <p style={{fontSize:11,fontWeight:500,color:"#98a2b3",marginBottom:8,textTransform:"uppercase",letterSpacing:".5px"}}>Дата</p>
        <button onClick={()=>setsCal(!sCal)} style={{display:"flex",alignItems:"center",gap:8,padding:"9px 14px",borderRadius:11,background:"#f8f9fb",border:sCal?"1px solid #3b82f6":"1px solid #e8eaed",fontSize:13,color:"#344054",cursor:"pointer",fontFamily:"inherit",width:"100%",transition:"border .2s"}}>
          <span style={{color:"#3b82f6",display:"flex"}}>{IC.cal}</span>{fmtD(aDate)}{fmtD(aDate)===todayStr&&<span style={{fontSize:11,color:"#98a2b3"}}>(сегодня)</span>}
        </button>
        {sCal&&<CalPick sel={aDate} onSel={d=>{setADate(d);setsCal(false);}} onClose={()=>setsCal(false)}/>}
      </div>
      <button onClick={()=>{const a=parseFloat(aAmt)||0;if(a<=0)return;onAdd({type:aType,cat:aCat||"Другое",amount:aType==="expense"?-a:a,wallet:aWallet,date:fmtD(aDate),desc:aDesc},editTx?.id);onClose();}} style={{width:"100%",padding:"13px",borderRadius:11,background:aType==="expense"?"linear-gradient(135deg,#ef4444,#dc2626)":"linear-gradient(135deg,#22c55e,#16a34a)",color:"#fff",fontSize:14,fontWeight:600,border:"none",cursor:"pointer",fontFamily:"inherit",transition:"all .2s"}} onMouseEnter={e=>e.currentTarget.style.transform="translateY(-1px)"} onMouseLeave={e=>e.currentTarget.style.transform="translateY(0)"}>
        {editTx?"Сохранить изменения":(aType==="expense"?"Добавить расход":"Добавить доход")}
      </button>
    </div>
  </div>;
}

/* ====== TRANSACTION ROW ====== */
function TxRow({tx, search, catColors, selected, onSelect, onCtx, newId, ti}){
  const [removing,setRemoving]=useState(false);
  const isNew=tx.id===newId;
  const isSelected=selected.has(tx.id);

  return <div
    className="tr"
    onContextMenu={e=>onCtx(e,tx)}
    style={{
      display:"flex",alignItems:"center",padding:"11px 20px",borderBottom:"1px solid #f8f9fb",gap:11,
      background:removing?"transparent":isSelected?"#f0fdf4":isNew?"rgba(34,197,94,.04)":"transparent",
      animation:isNew?"highlight 2s ease":removing?"rowOut .28s ease forwards":`rowIn .2s ease both`,
      animationDelay:isNew||removing?"0s":`${ti*.025}s`,
      cursor:"default",
    }}>
    {/* Checkbox */}
    <div className="cb-wrap" onClick={e=>{e.stopPropagation();onSelect(tx.id);}} style={{width:16,height:16,borderRadius:5,border:isSelected?"2px solid #22c55e":"2px solid #d0d5dd",background:isSelected?"#22c55e":"transparent",display:"flex",alignItems:"center",justifyContent:"center",cursor:"pointer",flexShrink:0,transition:"all .15s"}}>
      {isSelected&&<svg width="9" height="9" viewBox="0 0 24 24" fill="none" stroke="#fff" strokeWidth="3.5" strokeLinecap="round" strokeLinejoin="round"><polyline points="20 6 9 17 4 12"/></svg>}
    </div>
    {/* Dot */}
    <div style={{width:8,height:8,borderRadius:"50%",background:tx.type==="income"?catColors[tx.cat]||"#22c55e":"#ef4444",flexShrink:0}}/>
    {/* Cat */}
    <span style={{fontSize:13,fontWeight:500,color:"#344054",flex:"0 0 160px",minWidth:0,overflow:"hidden",textOverflow:"ellipsis",whiteSpace:"nowrap"}}>
      <Highlight text={tx.cat} query={search}/>
    </span>
    {/* Desc — always visible column, placeholder dash when empty */}
    <span style={{fontSize:12,color:tx.desc?"#667085":"#d0d5dd",flex:1,overflow:"hidden",textOverflow:"ellipsis",whiteSpace:"nowrap",minWidth:0,fontStyle:tx.desc?"normal":"normal"}}>
      {tx.desc
        ? <Highlight text={tx.desc} query={search}/>
        : <span style={{letterSpacing:1}}>—</span>
      }
    </span>
    {/* Wallet */}
    <span style={{display:"flex",color:tx.wallet==="cash"?"#b0b8c4":"#93c5fd",flexShrink:0}}>{tx.wallet==="cash"?IC.cash:IC.card}</span>
    {/* Amount */}
    <span style={{fontSize:13,fontWeight:600,fontFamily:"'JetBrains Mono',monospace",color:tx.type==="income"?"#22c55e":"#ef4444",textAlign:"right",flexShrink:0,minWidth:110}}>
      {tx.type==="income"?"+":"−"}{Math.abs(tx.amount).toLocaleString("ru-RU")} ₽
    </span>
  </div>;
}

/* ====== BULK ACTION BAR ====== */
function BulkBar({count, onDelete, onDeselect}){
  return <div style={{position:"fixed",bottom:28,left:"50%",transform:"translateX(-50%)",zIndex:80,background:"#1a1d23",color:"#fff",borderRadius:14,padding:"10px 18px",display:"flex",alignItems:"center",gap:14,boxShadow:"0 12px 40px rgba(0,0,0,.25)",animation:"toastIn .3s cubic-bezier(.34,1.56,.64,1)"}}>
    <span style={{fontSize:13,fontWeight:500}}>Выбрано: <b>{count}</b></span>
    <div style={{width:1,height:18,background:"rgba(255,255,255,.15)"}}/>
    <button onClick={onDelete} style={{display:"flex",alignItems:"center",gap:6,border:"none",background:"rgba(239,68,68,.15)",color:"#fca5a5",borderRadius:8,padding:"6px 12px",fontSize:12,fontWeight:600,cursor:"pointer",fontFamily:"inherit",transition:"all .15s"}} onMouseEnter={e=>e.currentTarget.style.background="rgba(239,68,68,.25)"} onMouseLeave={e=>e.currentTarget.style.background="rgba(239,68,68,.15)"}>
      <span style={{display:"flex"}}>{IC.trash}</span>Удалить
    </button>
    <button onClick={onDeselect} style={{display:"flex",alignItems:"center",gap:4,border:"none",background:"transparent",color:"rgba(255,255,255,.5)",borderRadius:8,padding:"6px 8px",fontSize:12,cursor:"pointer",fontFamily:"inherit",transition:"color .15s"}} onMouseEnter={e=>e.currentTarget.style.color="rgba(255,255,255,.9)"} onMouseLeave={e=>e.currentTarget.style.color="rgba(255,255,255,.5)"}>
      <span style={{display:"flex"}}>{IC.x}</span>Снять
    </button>
  </div>;
}

/* ====== MAIN ====== */
export default function OperationsScreen(){
  const [txList,setTxList]=useState(initialTx);
  const [cats]=useState(initCats);
  const [modal,setModal]=useState(false);
  const [editTx,setEditTx]=useState(null);
  const [ctx,setCtx]=useState(null);
  const [toasts,setToasts]=useState([]);
  const [newId,setNewId]=useState(null);
  const [visibleCount,setVisibleCount]=useState(20);
  const [selected,setSelected]=useState(new Set());
  const [inlineAddDay,setInlineAddDay]=useState(null);
  const scrollRef=useRef(null);
  const [isFiltering,setIsFiltering]=useState(false);

  // Filters
  const [search,setSearch]=useState("");
  const [filterType,setFilterType]=useState("all");
  const [filterWallet,setFilterWallet]=useState("all");
  const [filterCat,setFilterCat]=useState("all");
  const [dateFrom,setDateFrom]=useState(null);
  const [dateTo,setDateTo]=useState(null);

  const absMax=useMemo(()=>Math.max(...txList.map(t=>Math.abs(t.amount)),1000),[txList]);
  const [amtMin,setAmtMin]=useState(0);
  const [amtMax,setAmtMax]=useState(absMax);
  // sync amtMax when absMax changes
  useEffect(()=>setAmtMax(a=>a===absMax?absMax:a),[absMax]);

  const catColors=useMemo(()=>{const m=Object.fromEntries(cats.map(c=>[c.name,c.color]));Object.assign(m,extraCatColors);return m;},[cats]);
  const allCatNames=useMemo(()=>[...new Set(txList.map(t=>t.cat))].sort(),[txList]);

  // Skeleton on filter change
  const filterKey=`${search}|${filterType}|${filterWallet}|${filterCat}|${dateFrom}|${dateTo}|${amtMin}|${amtMax}`;
  const prevKey=useRef(filterKey);
  useEffect(()=>{
    if(prevKey.current===filterKey)return;
    prevKey.current=filterKey;
    setIsFiltering(true);
    const t=setTimeout(()=>setIsFiltering(false),380);
    return()=>clearTimeout(t);
  },[filterKey]);

  const filtered=useMemo(()=>txList.filter(t=>{
    if(filterType!=="all"&&t.type!==filterType)return false;
    if(filterWallet!=="all"&&t.wallet!==filterWallet)return false;
    if(filterCat!=="all"&&t.cat!==filterCat)return false;
    const abs=Math.abs(t.amount);
    if(abs<amtMin||abs>amtMax)return false;
    if(search&&!t.cat.toLowerCase().includes(search.toLowerCase())&&!t.desc.toLowerCase().includes(search.toLowerCase()))return false;
    if(dateFrom){const[d,m,y]=t.date.split(".");if(new Date(y,m-1,d)<dateFrom)return false;}
    if(dateTo){const[d,m,y]=t.date.split(".");const te=new Date(dateTo);te.setHours(23,59,59);if(new Date(y,m-1,d)>te)return false;}
    return true;
  }),[txList,filterType,filterWallet,filterCat,search,dateFrom,dateTo,amtMin,amtMax]);

  const {groups,sortedDays}=useMemo(()=>{
    const g={};filtered.forEach(t=>{if(!g[t.date])g[t.date]=[];g[t.date].push(t);});
    const sd=Object.keys(g).sort((a,b)=>{const[ad,am,ay]=a.split(".");const[bd,bm,by]=b.split(".");return new Date(by,bm-1,bd)-new Date(ay,am-1,ad);});
    return{groups:g,sortedDays:sd};
  },[filtered]);

  const dayLabel=ds=>{
    if(ds===todayStr)return"Сегодня";
    const[d,m,y]=ds.split(".");const dt=new Date(y,m-1,d);
    const diff=Math.floor((today-dt)/86400000);
    if(diff===1)return"Вчера";
    const mo=["янв","фев","мар","апр","мая","июн","июл","авг","сен","окт","ноя","дек"];
    return`${d} ${mo[parseInt(m)-1]}`;
  };

  // Infinite scroll
  useEffect(()=>{
    const el=scrollRef.current;if(!el)return;
    const h=()=>{if(el.scrollTop+el.clientHeight>=el.scrollHeight-80)setVisibleCount(c=>c+15);};
    el.addEventListener("scroll",h);return()=>el.removeEventListener("scroll",h);
  },[]);
  useEffect(()=>setVisibleCount(20),[filterKey]);

  let countSeen=0;
  const visibleDays=[];
  for(const day of sortedDays){
    if(countSeen>=visibleCount)break;
    visibleDays.push(day);
    countSeen+=groups[day].length;
  }

  const totalInc=filtered.filter(t=>t.type==="income").reduce((s,t)=>s+t.amount,0);
  const totalExp=Math.abs(filtered.filter(t=>t.type==="expense").reduce((s,t)=>s+t.amount,0));

  const hasFilters=filterType!=="all"||filterWallet!=="all"||filterCat!=="all"||search||dateFrom||dateTo||(amtMin>0)||(amtMax<absMax);
  const resetFilters=()=>{setFilterType("all");setFilterWallet("all");setFilterCat("all");setSearch("");setDateFrom(null);setDateTo(null);setAmtMin(0);setAmtMax(absMax);};

  const toast=useCallback((a,ti,m,u)=>{const id=++tid;setToasts(p=>[...p,{id,action:a,title:ti,message:m,undoData:u}]);},[]);
  const dismiss=useCallback(id=>setToasts(p=>p.filter(t=>t.id!==id)),[]);
  const undo=useCallback(t=>{
    if(t.undoData?.type==="delete")setTxList(p=>[...p,...(Array.isArray(t.undoData.txs)?t.undoData.txs:[t.undoData.tx])].sort((a,b)=>b.id-a.id));
    else if(t.undoData?.type==="add")setTxList(p=>p.filter(x=>x.id!==t.undoData.txId));
    toast("add","Отменено","Операция восстановлена",null);
  },[toast]);

  const del=useCallback(tx=>{
    setTxList(p=>p.filter(t=>t.id!==tx.id));
    toast("delete","Удалено",`${tx.cat} — ${Math.abs(tx.amount).toLocaleString("ru-RU")} ₽`,{type:"delete",tx});
  },[toast]);

  const bulkDelete=useCallback(()=>{
    const txs=txList.filter(t=>selected.has(t.id));
    setTxList(p=>p.filter(t=>!selected.has(t.id)));
    setSelected(new Set());
    toast("delete",`Удалено ${txs.length} записей`,"",{type:"delete",txs});
  },[txList,selected,toast]);

  const handleAdd=useCallback((data,existingId)=>{
    if(existingId){
      setTxList(p=>p.map(t=>t.id===existingId?{...t,...data}:t));
      toast("edit","Изменено",`${data.cat} — ${Math.abs(data.amount).toLocaleString("ru-RU")} ₽`,null);
    } else {
      const n={id:Date.now(),...data};
      setTxList(p=>[n,...p]);setNewId(n.id);setTimeout(()=>setNewId(null),2200);
      toast("add","Добавлено",`${n.cat} — ${Math.abs(n.amount).toLocaleString("ru-RU")} ₽`,{type:"add",txId:n.id});
    }
  },[toast]);

  const onCtx=useCallback((e,tx)=>{e.preventDefault();setCtx({x:e.clientX,y:e.clientY,tx});},[]);
  const toggleSelect=useCallback(id=>setSelected(s=>{const n=new Set(s);n.has(id)?n.delete(id):n.add(id);return n;}),[]);

  return <div style={{display:"flex",height:"100vh",fontFamily:"'DM Sans','Segoe UI',sans-serif",background:"#f0f2f5",color:"#1a1d23",overflow:"hidden"}}>
    <style>{`
      @import url('https://fonts.googleapis.com/css2?family=DM+Sans:ital,opsz,wght@0,9..40,300;0,9..40,400;0,9..40,500;0,9..40,600;0,9..40,700&family=JetBrains+Mono:wght@400;500;600;700&display=swap');
      *{box-sizing:border-box;margin:0;padding:0}
      ::-webkit-scrollbar{width:5px}::-webkit-scrollbar-track{background:transparent}::-webkit-scrollbar-thumb{background:#d0d5dd;border-radius:3px}
      @keyframes fadeIn{from{opacity:0;transform:translateY(6px)}to{opacity:1;transform:translateY(0)}}
      @keyframes scaleIn{from{opacity:0;transform:scale(.93)}to{opacity:1;transform:scale(1)}}
      @keyframes modalIn{from{opacity:0;transform:scale(.92) translateY(12px)}to{opacity:1;transform:scale(1) translateY(0)}}
      @keyframes backdropIn{from{opacity:0}to{opacity:1}}
      @keyframes tipIn{from{opacity:0;transform:translateX(-4px) translateY(-50%)}to{opacity:1;transform:translateX(0) translateY(-50%)}}
      @keyframes toastIn{from{opacity:0;transform:translateY(16px) scale(.9)}to{opacity:1;transform:translateY(0) scale(1)}}
      @keyframes toastOut{from{opacity:1}to{opacity:0;transform:translateY(-8px) scale(.95)}}
      @keyframes rowIn{from{opacity:0;transform:translateX(-8px)}to{opacity:1;transform:translateX(0)}}
      @keyframes rowOut{from{opacity:1;transform:translateX(0);max-height:60px}to{opacity:0;transform:translateX(-40px);max-height:0;padding:0;margin:0}}
      @keyframes highlight{0%{background:rgba(34,197,94,.1)}100%{background:transparent}}
      @keyframes skPulse{0%,100%{opacity:.4}50%{opacity:.9}}
      .ni{transition:all .2s;cursor:pointer;position:relative}.ni:hover{background:rgba(255,255,255,.08)!important}
      .ni.a{background:rgba(255,255,255,.12)!important}.ni.a::before{content:'';position:absolute;left:0;top:50%;transform:translateY(-50%);width:3px;height:22px;background:#22c55e;border-radius:0 3px 3px 0}
      .tr{transition:background .12s;cursor:default}.tr:hover .cb-wrap{border-color:#22c55e !important;opacity:1 !important}
      .fab{transition:all .2s cubic-bezier(.34,1.56,.64,1);cursor:pointer}.fab:hover{transform:scale(1.08);box-shadow:0 8px 28px rgba(34,197,94,.4)!important}.fab:active{transform:scale(.95)}
      .chip{transition:all .15s;cursor:pointer;border:none;font-family:inherit}.chip:hover{opacity:.85}
      .sk{background:#f0f2f5;animation:skPulse 1.4s ease-in-out infinite}
      .range-thumb-l, .range-thumb-r{pointer-events:auto}
      input[type=range]{-webkit-appearance:none;background:transparent;width:100%}
      input[type=range]::-webkit-slider-thumb{-webkit-appearance:none;width:16px;height:16px;border-radius:50%;background:#7c3aed;cursor:pointer;border:2px solid #fff;box-shadow:0 1px 4px rgba(0,0,0,.15);pointer-events:auto}
      input[type=range]::-moz-range-thumb{width:16px;height:16px;border-radius:50%;background:#7c3aed;cursor:pointer;border:2px solid #fff}
      mark{background:#fef08a;border-radius:2px;padding:0 1px}
    `}</style>

    {/* SIDEBAR */}
    <nav style={{width:64,background:"linear-gradient(180deg,#1a1d23,#24272e)",display:"flex",flexDirection:"column",flexShrink:0,zIndex:10}}>
      <div style={{padding:"18px 0",display:"flex",alignItems:"center",justifyContent:"center",borderBottom:"1px solid rgba(255,255,255,.06)"}}>
        <div style={{width:34,height:34,borderRadius:9,background:"linear-gradient(135deg,#22c55e,#16a34a)",display:"flex",alignItems:"center",justifyContent:"center",fontSize:15,fontWeight:700,color:"#fff"}}>₽</div>
      </div>
      <div style={{padding:"10px 7px",flex:1,display:"flex",flexDirection:"column",gap:2}}>
        {navItems.map(i=><Tip key={i.label} text={i.label}><div className={`ni ${i.active?"a":""}`} style={{display:"flex",alignItems:"center",justifyContent:"center",padding:"9px 0",borderRadius:9,color:i.active?"#fff":"rgba(255,255,255,.45)"}}>{IC[i.icon]}</div></Tip>)}
      </div>
      <div style={{padding:"8px 7px 14px",display:"flex",flexDirection:"column",gap:6,alignItems:"center"}}>
        <Tip text="Заблокировать"><button style={{border:"none",background:"rgba(255,255,255,.06)",borderRadius:8,width:34,height:34,display:"flex",alignItems:"center",justifyContent:"center",color:"rgba(255,255,255,.35)",cursor:"pointer",transition:"all .2s"}} onMouseEnter={e=>{e.currentTarget.style.background="rgba(255,255,255,.12)";e.currentTarget.style.color="rgba(255,255,255,.6)";}} onMouseLeave={e=>{e.currentTarget.style.background="rgba(255,255,255,.06)";e.currentTarget.style.color="rgba(255,255,255,.35)";}}>{IC.lock}</button></Tip>
      </div>
    </nav>

    {/* MAIN */}
    <main style={{flex:1,overflow:"hidden",display:"flex",flexDirection:"column",minWidth:0}}>

      {/* TOP BAR */}
      <div style={{padding:"20px 28px 0",flexShrink:0,animation:"fadeIn .3s"}}>
        <div style={{display:"flex",alignItems:"center",justifyContent:"space-between",marginBottom:16}}>
          <h1 style={{fontSize:24,fontWeight:700,letterSpacing:"-.5px"}}>Операции</h1>
          <div style={{display:"flex",alignItems:"center",gap:10}}>
            <div style={{display:"flex",alignItems:"center",gap:6,padding:"6px 14px",borderRadius:20,background:"#f0fdf4",border:"1px solid #bbf7d0"}}>
              <span style={{display:"flex",color:"#22c55e"}}>{IC.arrowUp}</span>
              <ANum value={totalInc} color="#15803d" size={13}/>
            </div>
            <div style={{display:"flex",alignItems:"center",gap:6,padding:"6px 14px",borderRadius:20,background:"#fef2f2",border:"1px solid #fecaca"}}>
              <span style={{display:"flex",color:"#ef4444"}}>{IC.arrowDown}</span>
              <ANum value={totalExp} color="#dc2626" size={13}/>
            </div>
          </div>
        </div>

        {/* FILTER BAR */}
        <div style={{background:"#fff",borderRadius:"14px 14px 0 0",border:"1px solid #e8eaed",borderBottom:"none",padding:"12px 18px",display:"flex",alignItems:"center",gap:10,flexWrap:"wrap",animation:"fadeIn .4s"}}>
          {/* Search */}
          <div style={{display:"flex",alignItems:"center",gap:7,padding:"7px 11px",borderRadius:9,background:"#f8f9fb",border:"1px solid #e8eaed",flex:"1 1 180px",minWidth:140,maxWidth:240,transition:"border .2s"}} onFocusCapture={e=>e.currentTarget.style.borderColor="#3b82f6"} onBlurCapture={e=>e.currentTarget.style.borderColor="#e8eaed"}>
            <span style={{color:"#98a2b3",display:"flex",flexShrink:0}}>{IC.search}</span>
            <input value={search} onChange={e=>setSearch(e.target.value)} placeholder="Поиск..." style={{border:"none",outline:"none",background:"transparent",fontSize:12,color:"#344054",width:"100%",fontFamily:"inherit"}}/>
            {search&&<button onClick={()=>setSearch("")} style={{border:"none",background:"none",cursor:"pointer",display:"flex",color:"#98a2b3",flexShrink:0,padding:0}} onMouseEnter={e=>e.currentTarget.style.color="#ef4444"} onMouseLeave={e=>e.currentTarget.style.color="#98a2b3"}>{IC.x}</button>}
          </div>

          {/* Date range with presets */}
          <DateRangeFilter from={dateFrom} to={dateTo} onFromChange={setDateFrom} onToChange={setDateTo}/>

          {/* Type chips */}
          <div style={{display:"flex",gap:4,background:"#f8f9fb",borderRadius:9,padding:3,border:"1px solid #e8eaed",flexShrink:0}}>
            {[{v:"all",l:"Все"},{v:"income",l:"Доходы"},{v:"expense",l:"Расходы"}].map(({v,l})=><button key={v} className="chip" onClick={()=>setFilterType(v)} style={{padding:"5px 10px",borderRadius:7,fontSize:12,fontWeight:filterType===v?600:400,color:filterType===v?"#fff":"#667085",background:filterType===v?(v==="income"?"#22c55e":v==="expense"?"#ef4444":"#1a1d23"):"transparent"}}>{l}</button>)}
          </div>

          {/* Wallet chips */}
          <div style={{display:"flex",gap:4,background:"#f8f9fb",borderRadius:9,padding:3,border:"1px solid #e8eaed",flexShrink:0}}>
            {[{v:"all",l:"Все"},{v:"cash",l:"Нал"},{v:"card",l:"Безнал"}].map(({v,l})=><button key={v} className="chip" onClick={()=>setFilterWallet(v)} style={{padding:"5px 10px",borderRadius:7,fontSize:12,fontWeight:filterWallet===v?600:400,color:filterWallet===v?"#fff":"#667085",background:filterWallet===v?"#3b82f6":"transparent"}}>{l}</button>)}
          </div>

          {/* Category combobox */}
          <CatComboBox value={filterCat} onChange={setFilterCat} catNames={allCatNames} catColors={catColors}/>

          {/* Amount range */}
          <AmountFilter min={amtMin} max={amtMax} onMin={setAmtMin} onMax={setAmtMax} absMax={absMax}/>

          {/* Reset */}
          {hasFilters&&<button onClick={resetFilters} style={{display:"flex",alignItems:"center",gap:5,padding:"7px 11px",borderRadius:9,background:"#fef2f2",border:"1px solid #fecaca",fontSize:12,color:"#ef4444",fontWeight:500,cursor:"pointer",fontFamily:"inherit",transition:"all .15s",flexShrink:0}} onMouseEnter={e=>e.currentTarget.style.background="#fee2e2"} onMouseLeave={e=>e.currentTarget.style.background="#fef2f2"}>
            <span style={{display:"flex"}}>{IC.x}</span>Сбросить
          </button>}
        </div>
      </div>

      {/* LIST */}
      <div ref={scrollRef} style={{flex:1,overflow:"auto",background:"#fff",border:"1px solid #e8eaed",borderTop:"none",borderRadius:"0 0 14px 14px",margin:"0 28px"}}>
        {isFiltering
          ? <div style={{animation:"fadeIn .2s"}}><SkeletonGroup/><SkeletonGroup/></div>
          : filtered.length===0
            ? <div style={{display:"flex",flexDirection:"column",alignItems:"center",justifyContent:"center",padding:"80px 20px",gap:14,animation:"fadeIn .4s"}}>
                <div style={{width:52,height:52,borderRadius:14,background:"#f0f2f5",display:"flex",alignItems:"center",justifyContent:"center",color:"#d0d5dd"}}>{IC.search}</div>
                <div style={{textAlign:"center"}}><p style={{fontSize:15,fontWeight:600,color:"#344054",marginBottom:4}}>Операции не найдены</p><p style={{fontSize:13,color:"#98a2b3"}}>Попробуйте изменить фильтры</p></div>
                {hasFilters&&<button onClick={resetFilters} style={{padding:"8px 20px",borderRadius:9,background:"#f0f2f5",border:"none",fontSize:13,color:"#667085",cursor:"pointer",fontFamily:"inherit",fontWeight:500}}>Сбросить</button>}
              </div>
            : <>
                {visibleDays.map((day,di)=>{
                  const dayTx=groups[day];
                  const dayInc=dayTx.filter(t=>t.type==="income").reduce((s,t)=>s+t.amount,0);
                  const dayExp=Math.abs(dayTx.filter(t=>t.type==="expense").reduce((s,t)=>s+t.amount,0));
                  const dayD=dayInc-dayExp;
                  return <div key={day} style={{animation:`fadeIn ${.12+di*.04}s ease both`}}>
                    {/* Day header */}
                    <div style={{display:"flex",justifyContent:"space-between",alignItems:"center",padding:"9px 20px 7px",background:"#f8f9fb",borderBottom:"1px solid #f0f2f5",borderTop:di>0?"1px solid #f0f2f5":"none",position:"sticky",top:0,zIndex:2}}>
                      <div style={{display:"flex",alignItems:"center",gap:10}}>
                        <span style={{fontSize:12,fontWeight:600,color:"#344054"}}>{dayLabel(day)}</span>
                        {/* Inline add button */}
                        {inlineAddDay!==day&&<button onClick={()=>setInlineAddDay(day)} style={{display:"flex",alignItems:"center",gap:3,padding:"2px 8px",borderRadius:6,border:"1px dashed #d0d5dd",background:"transparent",fontSize:11,color:"#98a2b3",cursor:"pointer",fontFamily:"inherit",transition:"all .15s"}} onMouseEnter={e=>{e.currentTarget.style.borderColor="#22c55e";e.currentTarget.style.color="#22c55e";}} onMouseLeave={e=>{e.currentTarget.style.borderColor="#d0d5dd";e.currentTarget.style.color="#98a2b3";}}>
                          <span style={{display:"flex"}}>{IC.plus}</span>добавить
                        </button>}
                      </div>
                      <span style={{fontSize:11,fontWeight:600,fontFamily:"'JetBrains Mono',monospace",color:dayD>=0?"#22c55e":"#ef4444"}}>{fm(dayD)} ₽</span>
                    </div>
                    {/* Inline add row */}
                    {inlineAddDay===day&&<InlineAddRow date={day} cats={cats} catColors={catColors} onAdd={tx=>{setTxList(p=>[tx,...p]);setNewId(tx.id);setTimeout(()=>setNewId(null),2200);toast("add","Добавлено",`${tx.cat} — ${Math.abs(tx.amount).toLocaleString("ru-RU")} ₽`,{type:"add",txId:tx.id});}} onClose={()=>setInlineAddDay(null)}/>}
                    {/* Transactions */}
                    {dayTx.map((tx,ti)=><TxRow key={tx.id} tx={tx} search={search} catColors={catColors} selected={selected} onSelect={toggleSelect} onCtx={onCtx} newId={newId} ti={ti}/>)}
                  </div>;
                })}
                <div style={{padding:"12px 20px",textAlign:"center",borderTop:"1px solid #f0f2f5"}}>
                  <span style={{fontSize:11,color:"#98a2b3"}}>
                    Показано {visibleDays.reduce((s,d)=>s+(groups[d]?.length||0),0)} из {filtered.length}
                    {filtered.length<txList.length&&<span style={{color:"#d0d5dd"}}> (всего {txList.length})</span>}
                  </span>
                </div>
              </>
        }
      </div>

      <div style={{padding:"6px 28px 10px",flexShrink:0}}>
        <p style={{fontSize:11,color:"#98a2b3",textAlign:"center"}}>ПКМ → Редактировать · Дублировать · Удалить &nbsp;·&nbsp; Hover → выбор записей</p>
      </div>
    </main>

    {/* FAB */}
    <button className="fab" onClick={()=>{setEditTx(null);setModal(true);}} style={{position:"fixed",bottom:24,right:28,width:52,height:52,borderRadius:15,background:"linear-gradient(135deg,#22c55e,#16a34a)",color:"#fff",border:"none",display:"flex",alignItems:"center",justifyContent:"center",boxShadow:"0 4px 18px rgba(34,197,94,.3)",zIndex:selected.size>0?0:50,cursor:"pointer",opacity:selected.size>0?0:1,transition:"opacity .2s, transform .2s"}}>
      {IC.plus}
    </button>

    {/* BULK BAR */}
    {selected.size>0&&<BulkBar count={selected.size} onDelete={bulkDelete} onDeselect={()=>setSelected(new Set())}/>}

    {/* CTX MENU */}
    {ctx&&<CtxMenu x={ctx.x} y={ctx.y} onEdit={()=>{setEditTx(ctx.tx);setModal(true);}} onDup={()=>{const d={...ctx.tx,id:Date.now()};setTxList(p=>[d,...p]);setNewId(d.id);setTimeout(()=>setNewId(null),2200);toast("add","Дублировано",`${d.cat} — ${Math.abs(d.amount).toLocaleString("ru-RU")} ₽`,{type:"add",txId:d.id});}} onDel={()=>del(ctx.tx)} onClose={()=>setCtx(null)}/>}

    {/* MODAL */}
    {modal&&<TxModal cats={cats} catColors={catColors} onAdd={handleAdd} onClose={()=>{setModal(false);setEditTx(null);}} editTx={editTx}/>}

    {/* TOASTS */}
    <Toasts toasts={toasts} onDismiss={dismiss} onUndo={undo}/>
  </div>;
}
