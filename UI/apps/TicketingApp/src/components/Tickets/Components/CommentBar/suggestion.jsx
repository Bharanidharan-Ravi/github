import { ReactRenderer } from '@tiptap/react'
import tippy from 'tippy.js'
import React, { forwardRef, useEffect, useImperativeHandle, useState } from 'react'

// 1. The Dropdown List Component
const MentionList = forwardRef((props, ref) => {
  const [selectedIndex, setSelectedIndex] = useState(0)

  const selectItem = index => {
    const item = props.items[index]
    if (item) {
      // CRITICAL: We pass the whole item (id, name, color) to the editor
      props.command({ id: item.id, label: item.name, color: item.color || null })
    }
  }

  useImperativeHandle(ref, () => ({
    onKeyDown: ({ event }) => {
      if (event.key === 'ArrowUp') {
        setSelectedIndex((selectedIndex + props.items.length - 1) % props.items.length)
        return true
      }
      if (event.key === 'ArrowDown') {
        setSelectedIndex((selectedIndex + 1) % props.items.length)
        return true
      }
      if (event.key === 'Enter') {
        selectItem(selectedIndex)
        return true
      }
      return false
    },
  }))

  useEffect(() => setSelectedIndex(0), [props.items])

  return (
    <div className="suggestion-list">
      {props.items.length ? (
        props.items.map((item, index) => (
          <button
            className={index === selectedIndex ? 'is-selected' : ''}
            key={index}
            onClick={() => selectItem(index)}
          >
            {/* Show Color dot for labels */}
            {item.color && (
              <span 
                style={{display:'inline-block', width:'10px', height:'10px', borderRadius:'50%', backgroundColor: item.color, marginRight:'8px'}} 
              ></span>
            )}
            {item.name}
          </button>
        ))
      ) : (
        <div className="item">No result</div>
      )}
    </div>
  )
})

// 2. The Configuration Generator
// We wrap this in a function so we can pass different lists (users vs labels)
export const createSuggestion = (itemsList) => {
  return {
    items: ({ query }) => {
      return itemsList
        .filter(item => item.name.toLowerCase().startsWith(query.toLowerCase()))
        .slice(0, 5) // Limit to top 5 results
    },
    render: () => {
      let component
      let popup

      return {
        onStart: props => {
          component = new ReactRenderer(MentionList, {
            props,
            editor: props.editor,
          })

          if (!props.clientRect) return

          popup = tippy('body', {
            getReferenceClientRect: props.clientRect,
            appendTo: () => document.body,
            content: component.element,
            showOnCreate: true,
            interactive: true,
            trigger: 'manual',
            placement: 'bottom-start',
          })
        },
        onUpdate(props) {
          component.updateProps(props)
          if (!props.clientRect) return
          popup[0].setProps({
            getReferenceClientRect: props.clientRect,
          })
        },
        onKeyDown(props) {
          if (props.event.key === 'Escape') {
            popup[0].hide()
            return true
          }
          return component.ref?.onKeyDown(props)
        },
        onExit() {
          if (popup) popup[0].destroy()
          if (component) component.destroy()
        },
      }
    },
  }
}