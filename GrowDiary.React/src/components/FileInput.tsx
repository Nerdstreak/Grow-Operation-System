import { useId, useRef } from 'react'
import { V1Button } from './v1'

type FileInputProps = {
  accept?: string
  disabled?: boolean
  fileNames?: string[]
  label?: string
  multiple?: boolean
  onFiles: (files: File[]) => void
}

function FileInput({ accept, disabled = false, fileNames = [], label = 'Datei auswählen', multiple = false, onFiles }: FileInputProps) {
  const id = useId()
  const inputRef = useRef<HTMLInputElement | null>(null)
  const summary = fileNames.length === 0 ? 'Keine Datei ausgewählt' : multiple ? `${fileNames.length} Dateien ausgewählt` : fileNames[0]

  return (
    <div className="rc-file-input">
      <input
        ref={inputRef}
        id={id}
        className="rc-file-input-native"
        type="file"
        accept={accept}
        disabled={disabled}
        multiple={multiple}
        onChange={(event) => onFiles(Array.from(event.target.files ?? []))}
      />
      <V1Button disabled={disabled} onClick={() => inputRef.current?.click()}>{label}</V1Button>
      <span className="rc-file-input-name">{summary}</span>
    </div>
  )
}

export default FileInput
